using System.Diagnostics;
using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Validation.Checks;

// Spec 008 / research §5. Shared span + duration + result-shaping helper.
// Five check implementations (Existence, Accessibility, RequiredPermissions,
// IdentityAuthorization, ApiReachability) all follow the same pattern:
//   1) Start the per-check child Activity under the runner's parent span.
//   2) Stopwatch the probe call.
//   3) Map the probe result to a ValidationCheckResult.
//   4) Set OTel attributes on the child span before disposing.
// Wrapping this once keeps each check class tight and idiomatic — no
// duplicated timing or span-attribute boilerplate.
internal static class CheckExecutor
{
    public static async Task<CheckExecutionResult> RunAsync(
        ValidationCheckName name,
        Func<CancellationToken, Task<ArmProbeResult>> probe,
        CancellationToken cancellationToken)
    {
        using var activity = NamespaceValidationActivitySource.StartCheckSpan(name, Activity.Current);
        var stopwatch = Stopwatch.StartNew();

        ArmProbeResult probeResult;
        try
        {
            probeResult = await probe(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var cancelled = new ValidationCheckResult(
                name,
                ValidationCheckOutcome.Fail,
                "Timeout",
                ValidationFailureCategory.Timeout,
                (int)stopwatch.ElapsedMilliseconds);
            activity.SetCheckOutcome(cancelled.Outcome, cancelled.ReasonCategory, cancelled.DurationMs);
            throw;
        }

        stopwatch.Stop();
        var result = new ValidationCheckResult(
            Name: name,
            Outcome: probeResult.Outcome,
            Reason: probeResult.Reason,
            ReasonCategory: probeResult.ReasonCategory,
            DurationMs: (int)stopwatch.ElapsedMilliseconds,
            CorrelationRequestId: probeResult.CorrelationRequestId);

        activity.SetCheckOutcome(
            result.Outcome,
            result.ReasonCategory,
            result.DurationMs,
            result.CorrelationRequestId);

        return new CheckExecutionResult(result, probeResult.Snapshot);
    }
}
