using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Features.Discovery.Shared.Telemetry;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Api.Features.Discovery.Shared;

// Spec 009 / T043 + FR-003. Wraps the per-namespace lock acquire + the
// DiscoveryRun create into a single coalescing call. The endpoint layer asks
// for "the active run for this namespace, starting a new one if needed" and
// gets back a typed result distinguishing fresh starts from coalesced ones.
//
// Algorithm:
//   1. Try to acquire the lock with a freshly-allocated runId.
//   2. If acquired (or stolen): create a DiscoveryRun in `Queued` state and
//      return (runId, coalesced=false). On Stolen, also mark the orphaned
//      prior run as Failed/WorkerLost.
//   3. If coalesced: append a CoalescedRequest to the in-flight run and
//      return (existingRunId, coalesced=true).
public interface IDiscoveryRunCoalescer
{
    Task<CoalescerResult> EnsureRunAsync(
        string namespaceId,
        string requestedBy,
        string correlationId,
        CancellationToken cancellationToken);

    // Issue #116 — compensation for a publish failure after EnsureRunAsync
    // returned a fresh run. Marks the run Failed (Transport/Enqueue) and
    // releases the per-namespace lock so the next request starts a new run
    // instead of coalescing onto a run no worker will ever process.
    Task AbandonQueuedRunAsync(
        string namespaceId,
        string runId,
        string reason,
        CancellationToken cancellationToken);
}

public sealed record CoalescerResult(
    string DiscoveryRunId,
    DiscoveryRunStatus Status,
    DateTimeOffset StartedUtc,
    bool CoalescedFromExisting);

public sealed partial class DiscoveryRunCoalescer : IDiscoveryRunCoalescer
{
    private readonly IDiscoveryLockStore _locks;
    private readonly IDiscoveryRunStore _runs;
    private readonly TimeProvider _time;
    private readonly DiscoveryMeter _meter;
    private readonly ILogger<DiscoveryRunCoalescer> _logger;

    public DiscoveryRunCoalescer(
        IDiscoveryLockStore locks,
        IDiscoveryRunStore runs,
        TimeProvider time,
        DiscoveryMeter meter,
        ILogger<DiscoveryRunCoalescer> logger)
    {
        _locks = locks;
        _runs = runs;
        _time = time;
        _meter = meter;
        _logger = logger;
    }

    public async Task<CoalescerResult> EnsureRunAsync(
        string namespaceId,
        string requestedBy,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);

        var now = _time.GetUtcNow();
        var newRunId = NewRunId();
        var podId = Environment.MachineName;

        var acquisition = await _locks.TryAcquireAsync(namespaceId, newRunId, podId, cancellationToken)
            .ConfigureAwait(false);

        switch (acquisition.Outcome)
        {
            case DiscoveryLockOutcome.Acquired:
            {
                var run = await CreateQueuedRunAsync(namespaceId, newRunId, requestedBy, correlationId, now, cancellationToken)
                    .ConfigureAwait(false);
                _meter.RunsStarted.Add(1, new KeyValuePair<string, object?>(DiscoveryMeter.TagOutcome, "new"));
                LogStarted(newRunId, namespaceId);
                return new CoalescerResult(run.Id, run.Status, run.StartedUtc, CoalescedFromExisting: false);
            }
            case DiscoveryLockOutcome.Stolen:
            {
                // Orphaned prior run — mark Failed/WorkerLost before we start.
                if (!string.IsNullOrEmpty(acquisition.StolenRunId))
                {
                    try
                    {
                        await _runs.UpdateStatusAsync(
                            acquisition.StolenRunId!, namespaceId,
                            new DiscoveryRunStatusUpdate(
                                Status: DiscoveryRunStatus.Failed,
                                CompletedUtc: now,
                                Failure: new DiscoveryRunFailure(
                                    Category: DiscoveryFailureCategory.WorkerLost,
                                    Message: "Lock expired; worker presumed lost.",
                                    OccurredAtPhase: DiscoveryPhase.LockAcquire,
                                    RetriesExhausted: null)),
                            ifMatch: null, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Microsoft.Azure.Cosmos.CosmosException ex)
                    {
                        // The orphan-cleanup is best-effort; don't block the
                        // legitimate new run if the prior record is missing or
                        // already terminal.
                        LogOrphanCleanupFailed(acquisition.StolenRunId!, ex.Message);
                    }
                    catch (InvalidOperationException ex)
                    {
                        LogOrphanCleanupFailed(acquisition.StolenRunId!, ex.Message);
                    }
                }
                var run = await CreateQueuedRunAsync(namespaceId, newRunId, requestedBy, correlationId, now, cancellationToken)
                    .ConfigureAwait(false);
                _meter.RunsStarted.Add(1, new KeyValuePair<string, object?>(DiscoveryMeter.TagOutcome, "new"));
                LogStartedAfterSteal(newRunId, namespaceId, acquisition.StolenRunId);
                return new CoalescerResult(run.Id, run.Status, run.StartedUtc, CoalescedFromExisting: false);
            }
            case DiscoveryLockOutcome.Coalesced:
            default:
            {
                var existing = await _runs.GetAsync(acquisition.ActiveRunId, namespaceId, cancellationToken)
                    .ConfigureAwait(false);
                if (existing is null)
                {
                    throw new InvalidOperationException(
                        $"DiscoveryRunCoalescer: lock points to runId {acquisition.ActiveRunId} but the run document is missing.");
                }
                await _runs.AppendCoalescedRequestAsync(
                    existing.Id, namespaceId,
                    new CoalescedRequest(now, requestedBy), cancellationToken).ConfigureAwait(false);
                _meter.RunsStarted.Add(1, new KeyValuePair<string, object?>(DiscoveryMeter.TagOutcome, "coalesced"));
                LogCoalesced(existing.Id, namespaceId);
                return new CoalescerResult(existing.Id, existing.Status, existing.StartedUtc, CoalescedFromExisting: true);
            }
        }
    }

    public async Task AbandonQueuedRunAsync(
        string namespaceId,
        string runId,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        // Both steps are best-effort: even if marking the run Failed loses a
        // race, the lock release is what prevents the wedge (a stuck Queued
        // run without the lock is cosmetic — the next request starts fresh).
        try
        {
            await _runs.UpdateStatusAsync(
                runId, namespaceId,
                new DiscoveryRunStatusUpdate(
                    Status: DiscoveryRunStatus.Failed,
                    CompletedUtc: _time.GetUtcNow(),
                    Failure: new DiscoveryRunFailure(
                        Category: DiscoveryFailureCategory.Transport,
                        Message: reason,
                        OccurredAtPhase: DiscoveryPhase.Enqueue,
                        RetriesExhausted: null)),
                ifMatch: null, cancellationToken).ConfigureAwait(false);
        }
        catch (Microsoft.Azure.Cosmos.CosmosException ex)
        {
            LogAbandonMarkFailed(runId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            LogAbandonMarkFailed(runId, ex.Message);
        }

        await _locks.ReleaseAsync(namespaceId, runId, cancellationToken).ConfigureAwait(false);
        _meter.RunsCompleted.Add(1,
            new KeyValuePair<string, object?>(DiscoveryMeter.TagStatus, "failed"),
            new KeyValuePair<string, object?>(DiscoveryMeter.TagFailureCategory, nameof(DiscoveryFailureCategory.Transport)));
        LogAbandoned(runId, namespaceId, reason);
    }

    private async Task<DiscoveryRun> CreateQueuedRunAsync(
        string namespaceId,
        string runId,
        string requestedBy,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var queued = new DiscoveryRun(
            Id: runId,
            SchemaVersion: DiscoveryRun.CurrentSchemaVersion,
            NamespaceId: namespaceId,
            Status: DiscoveryRunStatus.Queued,
            Trigger: DiscoveryTrigger.Manual,
            StartedUtc: now,
            CompletedUtc: null,
            DurationMs: null,
            RequestedBy: requestedBy,
            QueueCount: 0, TopicCount: 0, SubscriptionCount: 0, RuleCount: 0,
            NewCount: 0, UpdatedCount: 0, UnchangedCount: 0, MissingCount: 0,
            Failure: null,
            CoalescedRequests: Array.Empty<CoalescedRequest>(),
            CorrelationId: correlationId);
        return await _runs.CreateAsync(queued, cancellationToken).ConfigureAwait(false);
    }

    // R-07-style identifier: `dr_` + 26 chars of ULID-shaped base32. Cosmos
    // sees the value as an opaque id so a Guid-N suffix is fine for v1 —
    // upgrading to a true ULID is a follow-up if ordering ever matters.
    private static string NewRunId()
    {
        // 26 uppercased alphanumeric chars (max Cosmos id length within the
        // openapi.yaml pattern `^dr_[A-Z0-9]{26}$`).
        var guid = Guid.NewGuid().ToString("N").ToUpperInvariant(); // 32 chars
        return $"dr_{guid.AsSpan(0, 26)}";
    }

    [LoggerMessage(EventId = 9601, Level = LogLevel.Information,
        Message = "Discovery run {RunId} started for namespace {NamespaceId}.")]
    private partial void LogStarted(string runId, string namespaceId);

    [LoggerMessage(EventId = 9602, Level = LogLevel.Information,
        Message = "Discovery run {RunId} coalesced onto in-flight run for namespace {NamespaceId}.")]
    private partial void LogCoalesced(string runId, string namespaceId);

    [LoggerMessage(EventId = 9603, Level = LogLevel.Warning,
        Message = "Discovery run {RunId} started after stealing expired lock from {StolenRunId} (namespace {NamespaceId}).")]
    private partial void LogStartedAfterSteal(string runId, string namespaceId, string? stolenRunId);

    [LoggerMessage(EventId = 9604, Level = LogLevel.Warning,
        Message = "Failed to mark orphaned run {RunId} as WorkerLost: {Reason}")]
    private partial void LogOrphanCleanupFailed(string runId, string reason);

    [LoggerMessage(EventId = 9605, Level = LogLevel.Error,
        Message = "Discovery run {RunId} abandoned for namespace {NamespaceId}: {Reason}")]
    private partial void LogAbandoned(string runId, string namespaceId, string reason);

    [LoggerMessage(EventId = 9606, Level = LogLevel.Warning,
        Message = "Failed to mark abandoned run {RunId} as Failed/Enqueue: {Reason}")]
    private partial void LogAbandonMarkFailed(string runId, string reason);
}
