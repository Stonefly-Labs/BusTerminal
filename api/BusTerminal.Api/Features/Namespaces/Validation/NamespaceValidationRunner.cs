using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Namespaces.Validation.Checks;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Infrastructure.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Features.Namespaces.Validation;

// Spec 008 / T076 + research §5 + plan.md §Principle V. Orchestrator for the
// five named checks (Existence, Accessibility, RequiredPermissions,
// IdentityAuthorization, ApiReachability). Strategy:
//   - Start a parent OTel Activity (caller picks the parent span name —
//     "namespace.onboarding.run" for the wizard's pre-onboarding path,
//     "namespace.validation.rerun" for the details-page re-run).
//   - Fan the checks out via Task.WhenAll, each bounded by the
//     per-check timeout from ArmNamespaceProbeOptions (research §5 — 5s
//     per check, 3s for ApiReachability). The aggregate runner budget
//     (15s) is enforced by an outer linked CTS.
//   - Build a ValidationRun document with the aggregate status, optional
//     ARM snapshot (Existence-only), drift-detection diff against an
//     optional persisted namespace document, and persist append-only via
//     INamespaceValidationRunStore.
//
// Aggregate rules per data-model.md §2 ValidationStatus:
//   Healthy   = all five Pass
//   Degraded  = any non-fatal Fail, with Existence + Accessibility both Pass
//   Unhealthy = Existence or Accessibility Fail
public sealed partial class NamespaceValidationRunner
{
    private readonly IReadOnlyList<INamespaceValidationCheck> _checks;
    private readonly INamespaceValidationRunStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ArmNamespaceProbeOptions _options;
    private readonly ILogger<NamespaceValidationRunner> _logger;

    // Aggregate runner budget per research §5 / FR-039 — caps total wall-clock
    // even if every per-check timeout fires in series (which the parallel
    // execution makes impossible in normal operation but is the spec's
    // contract regardless).
    public static TimeSpan AggregateBudget => TimeSpan.FromSeconds(15);

    public NamespaceValidationRunner(
        IEnumerable<INamespaceValidationCheck> checks,
        INamespaceValidationRunStore store,
        TimeProvider timeProvider,
        IOptions<ArmNamespaceProbeOptions> options,
        ILogger<NamespaceValidationRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _checks = checks
            .OrderBy(c => (int)c.Name)
            .ToArray();
        _store = store;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;

        if (_checks.Count != 5)
        {
            throw new InvalidOperationException(
                $"NamespaceValidationRunner requires exactly 5 checks; got {_checks.Count}. " +
                "Spec 008 FR-014 fixes the check set at Existence, Accessibility, RequiredPermissions, IdentityAuthorization, ApiReachability.");
        }
    }

    public Task<ValidationRun> ExecuteAsync(NamespaceValidationRunRequest request, CancellationToken cancellationToken)
    {
        return ExecuteAsync(request, parentSpanName: NamespaceValidationActivitySource.ValidationRerunSpan, cancellationToken);
    }

    public async Task<ValidationRun> ExecuteAsync(
        NamespaceValidationRunRequest request,
        string parentSpanName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(parentSpanName);

        using var aggregate = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        aggregate.CancelAfter(AggregateBudget);

        using var parentSpan = NamespaceValidationActivitySource.Source.StartActivity(
            parentSpanName,
            ActivityKind.Internal);
        parentSpan?.SetTag("namespace.id", request.NamespaceId);
        if (!string.IsNullOrEmpty(request.Environment))
        {
            parentSpan?.SetTag("namespace.environment", request.Environment);
        }

        var startedAt = _timeProvider.GetUtcNow();
        var runStopwatch = Stopwatch.StartNew();

        var executionResults = await ExecuteChecksAsync(request.ArmId, aggregate.Token).ConfigureAwait(false);

        runStopwatch.Stop();

        var results = executionResults.Select(r => r.Result).ToArray();

        // Aggregate scoring per data-model.md §2.
        var aggregateStatus = ComputeAggregateStatus(results);

        // ARM snapshot captured by the probe when Existence passes (research §11).
        var snapshot = executionResults
            .FirstOrDefault(r => r.Result.Name == ValidationCheckName.Existence
                                  && r.Result.Outcome == ValidationCheckOutcome.Pass)
            ?.Snapshot;

        // Drift detection against the persisted namespace document, if any.
        var driftFields = ComputeDriftFields(snapshot, request.PersistedDriftBaseline);
        var driftDetected = driftFields.Count > 0;

        var run = new ValidationRun(
            Id: request.RunId,
            NamespaceId: request.NamespaceId,
            ExecutedAtUtc: startedAt,
            ExecutedBy: request.ExecutedBy,
            ExecutedByDisplayNameSnapshot: request.ExecutedByDisplayNameSnapshot,
            AzureResourceIdAtRun: request.ArmId.CanonicalArmId,
            AggregateStatus: aggregateStatus,
            CheckResults: results,
            DriftDetected: driftDetected,
            DriftFields: driftFields,
            TotalDurationMs: (int)runStopwatch.ElapsedMilliseconds,
            ArmResourceSnapshot: snapshot);

        parentSpan.SetAggregateStatus(aggregateStatus, driftDetected);

        await _store.AppendAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A single check raising must NOT poison the aggregate; the executor wraps probe failures into ValidationCheckResult but a malformed check implementation could still throw — defensive fallback to Unknown keeps the runner deterministic.")]
    private async Task<IReadOnlyList<CheckExecutionResult>> ExecuteChecksAsync(
        NamespaceArmId armId,
        CancellationToken cancellationToken)
    {
        var tasks = _checks
            .Select<INamespaceValidationCheck, Task<CheckExecutionResult>>(async check =>
            {
                try
                {
                    return await check.ExecuteAsync(armId, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return new CheckExecutionResult(
                        new ValidationCheckResult(
                            check.Name,
                            ValidationCheckOutcome.Fail,
                            "Timeout",
                            ValidationFailureCategory.Timeout,
                            DurationMs: 0),
                        Snapshot: null);
                }
                catch (Exception ex)
                {
                    LogCheckRaised(ex, check.Name.ToString(), ex.GetType().FullName ?? string.Empty);
                    return new CheckExecutionResult(
                        new ValidationCheckResult(
                            check.Name,
                            ValidationCheckOutcome.Fail,
                            "Unknown",
                            ValidationFailureCategory.Unknown,
                            DurationMs: 0),
                        Snapshot: null);
                }
            })
            .ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static ValidationStatus ComputeAggregateStatus(IReadOnlyList<ValidationCheckResult> results)
    {
        var existence = results.FirstOrDefault(r => r.Name == ValidationCheckName.Existence);
        var accessibility = results.FirstOrDefault(r => r.Name == ValidationCheckName.Accessibility);

        if (existence?.Outcome != ValidationCheckOutcome.Pass
            || accessibility?.Outcome != ValidationCheckOutcome.Pass)
        {
            return ValidationStatus.Unhealthy;
        }

        return results.All(r => r.Outcome == ValidationCheckOutcome.Pass)
            ? ValidationStatus.Healthy
            : ValidationStatus.Degraded;
    }

    private static IReadOnlyList<DriftField> ComputeDriftFields(
        ArmResourceSnapshot? snapshot,
        PersistedNamespaceBaseline? baseline)
    {
        if (snapshot is null || baseline is null)
        {
            return Array.Empty<DriftField>();
        }

        var drifts = new List<DriftField>(capacity: 3);
        if (!string.Equals(snapshot.Region, baseline.Region, StringComparison.OrdinalIgnoreCase))
        {
            drifts.Add(new DriftField("region", baseline.Region, snapshot.Region));
        }
        if (!string.Equals(snapshot.ResourceGroup, baseline.ResourceGroup, StringComparison.OrdinalIgnoreCase))
        {
            drifts.Add(new DriftField("resourceGroup", baseline.ResourceGroup, snapshot.ResourceGroup));
        }
        if (snapshot.SubscriptionId != baseline.SubscriptionId)
        {
            drifts.Add(new DriftField(
                "subscriptionId",
                baseline.SubscriptionId.ToString("D"),
                snapshot.SubscriptionId.ToString("D")));
        }
        return drifts;
    }

    [LoggerMessage(
        EventId = 8501,
        Level = LogLevel.Warning,
        Message = "Validation check {CheckName} raised an unexpected exception of type {ExceptionType}; aggregate marked Unknown.")]
    private partial void LogCheckRaised(Exception exception, string checkName, string exceptionType);
}

// Spec 008 / data-model.md §1.2. Input to NamespaceValidationRunner.
//
// `RunId` is pre-allocated by the caller — the endpoint stamps a new Guid
// per request. `NamespaceId` is the wizard-pre-allocated namespace Guid
// (research §18) so pre-onboarding runs partition-align with the eventual
// document. `PersistedDriftBaseline` is null on pre-onboarding runs (no
// persisted document yet) and populated on re-runs from the details page.
public sealed record NamespaceValidationRunRequest(
    Guid RunId,
    Guid NamespaceId,
    NamespaceArmId ArmId,
    string? Environment,
    Guid ExecutedBy,
    string ExecutedByDisplayNameSnapshot,
    PersistedNamespaceBaseline? PersistedDriftBaseline);

// Spec 008 / research §11. Subset of the persisted namespace document the
// runner needs to compute drift. The endpoint reads these from the
// persisted RegistryNamespace before invoking the runner so the runner stays
// store-agnostic.
public sealed record PersistedNamespaceBaseline(
    string Region,
    string ResourceGroup,
    Guid SubscriptionId);
