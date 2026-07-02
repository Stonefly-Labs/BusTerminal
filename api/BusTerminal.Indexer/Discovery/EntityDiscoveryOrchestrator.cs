// FR-021 requires the orchestrator to translate ANY upstream failure (ARM,
// Cosmos, transport, etc.) into a DiscoveryRunFailure record. That is why
// CA1031 is suppressed here — these `catch (Exception)` blocks are the
// failure-boundary the spec requires, not careless swallowing.
#pragma warning disable CA1031

using System.Collections.Generic;
using System.Diagnostics;
using BusTerminal.Indexer.Discovery.Classification;
using BusTerminal.Indexer.Discovery.Persistence;
using BusTerminal.Indexer.Discovery.Providers;
using BusTerminal.Indexer.Discovery.Telemetry;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Indexer.Discovery;

// Spec 009 / T053 + R-05 + FR-021 + FR-013/014.
// Orchestrates a single discovery run end-to-end:
//   1. Resolve the namespace context (ARM subscription / RG / FQNS).
//   2. Transition the DiscoveryRun to InProgress.
//   3. Stream entities through the four scopes (queues → topics →
//      subscriptions+rules), classifying + writing as they flow.
//   4. Track which scopes completed successfully. On any scope failure,
//      mark the run Failed (Failure category + phase set) and exit before
//      the missing-sweep — FR-021's partial-failure invariant: never flip a
//      scope's entities to Missing when that scope did not complete.
//   5. Missing-sweep: for each successfully-completed scope, find Active
//      entities whose `lastSeenUtc < runStartUtc` and flip to Missing.
//   6. Record final counts on the run.
public sealed partial class EntityDiscoveryOrchestrator
{
    private readonly IEntityDiscoveryProvider _provider;
    private readonly IPublishedEntityWriter _writer;
    private readonly IDiscoveryRunUpdater _runUpdater;
    private readonly INamespaceContextResolver _contextResolver;
    private readonly DiscoveryMeter _meter;
    private readonly TimeProvider _time;
    private readonly ILogger<EntityDiscoveryOrchestrator> _logger;

    public EntityDiscoveryOrchestrator(
        IEntityDiscoveryProvider provider,
        IPublishedEntityWriter writer,
        IDiscoveryRunUpdater runUpdater,
        INamespaceContextResolver contextResolver,
        DiscoveryMeter meter,
        TimeProvider time,
        ILogger<EntityDiscoveryOrchestrator> logger)
    {
        _provider = provider;
        _writer = writer;
        _runUpdater = runUpdater;
        _contextResolver = contextResolver;
        _meter = meter;
        _time = time;
        _logger = logger;
    }

    public async Task RunAsync(DiscoveryRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var rootSpan = DiscoveryActivitySource.Instance.StartActivity(
            DiscoveryActivitySource.SpanNames.Run, ActivityKind.Consumer);
        rootSpan?.SetTag(DiscoveryActivitySource.AttributeKeys.RunId, request.DiscoveryRunId);
        rootSpan?.SetTag(DiscoveryActivitySource.AttributeKeys.NamespaceId, request.NamespaceId);

        var startedUtc = _time.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();

        // Resolve the Azure coordinates we need to talk to ARM.
        NamespaceDiscoveryContext context;
        try
        {
            context = await _contextResolver.ResolveAsync(request.NamespaceId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RecordFailureAsync(request, startedUtc, stopwatch, ex,
                DiscoveryPhase.LockAcquire, DiscoveryFailureCategory.NotFound, cancellationToken).ConfigureAwait(false);
            return;
        }

        await _runUpdater.TransitionToInProgressAsync(request.DiscoveryRunId, request.NamespaceId, startedUtc, cancellationToken).ConfigureAwait(false);

        var providerContext = new EntityDiscoveryProviderContext(
            NamespaceId: request.NamespaceId,
            AzureSubscriptionId: context.AzureSubscriptionId,
            ResourceGroup: context.ResourceGroup,
            NamespaceName: context.NamespaceName);

        await using var batcher = new DiscoveryWriteBatcher(_writer,
            sp_NullLogger<DiscoveryWriteBatcher>.Instance);

        var completedScopes = new HashSet<DiscoveredEntityType>();
        var counts = new ScopeCounts();

        // Streams are processed sequentially across scopes; within each
        // scope we stream into the bounded batcher channel.
        var queuesOk = await StreamScopeAsync(
            DiscoveredEntityType.Queue, DiscoveryPhase.FetchQueues,
            _provider.StreamQueuesAsync(providerContext, cancellationToken), counts, batcher,
            request, startedUtc, cancellationToken).ConfigureAwait(false);
        if (!queuesOk.success)
        {
            await batcher.CompleteAsync().ConfigureAwait(false);
            await RecordFailureAsync(request, startedUtc, stopwatch, queuesOk.exception,
                DiscoveryPhase.FetchQueues, queuesOk.category, cancellationToken).ConfigureAwait(false);
            return;
        }
        completedScopes.Add(DiscoveredEntityType.Queue);

        var topicsOk = await StreamScopeAsync(
            DiscoveredEntityType.Topic, DiscoveryPhase.FetchTopics,
            _provider.StreamTopicsAsync(providerContext, cancellationToken), counts, batcher,
            request, startedUtc, cancellationToken).ConfigureAwait(false);
        if (!topicsOk.success)
        {
            await batcher.CompleteAsync().ConfigureAwait(false);
            await RecordFailureAsync(request, startedUtc, stopwatch, topicsOk.exception,
                DiscoveryPhase.FetchTopics, topicsOk.category, cancellationToken).ConfigureAwait(false);
            return;
        }
        completedScopes.Add(DiscoveredEntityType.Topic);

        var subsOk = await StreamScopeAsync(
            DiscoveredEntityType.Subscription, DiscoveryPhase.FetchSubscriptions,
            _provider.StreamSubscriptionsAndRulesAsync(providerContext, cancellationToken), counts, batcher,
            request, startedUtc, cancellationToken).ConfigureAwait(false);
        if (!subsOk.success)
        {
            await batcher.CompleteAsync().ConfigureAwait(false);
            await RecordFailureAsync(request, startedUtc, stopwatch, subsOk.exception,
                DiscoveryPhase.FetchSubscriptions, subsOk.category, cancellationToken).ConfigureAwait(false);
            return;
        }
        completedScopes.Add(DiscoveredEntityType.Subscription);
        completedScopes.Add(DiscoveredEntityType.Rule);

        await batcher.CompleteAsync().ConfigureAwait(false);
        var batchCounts = batcher.SnapshotCounts();

        // Missing-sweep — FR-014. For each successfully-completed scope, mark
        // Active entities not seen in this run as Missing. We sweep at the
        // namespace level and rely on lastSeenUtc < startedUtc.
        var missingCount = 0;
        try
        {
            await foreach (var candidate in _writer.ListMissingCandidatesAsync(
                request.NamespaceId, request.Environment, startedUtc, cancellationToken).ConfigureAwait(false))
            {
                await _writer.TransitionToMissingAsync(
                    candidate.EntityId, candidate.Environment, candidate.ETag,
                    startedUtc, request.DiscoveryRunId, cancellationToken).ConfigureAwait(false);
                missingCount++;
            }
        }
        catch (Exception ex)
        {
            await RecordFailureAsync(request, startedUtc, stopwatch, ex,
                DiscoveryPhase.Persist, DiscoveryFailureCategory.Internal, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Finalize.
        stopwatch.Stop();
        var completedUtc = _time.GetUtcNow();
        var durationMs = (int)stopwatch.ElapsedMilliseconds;
        var outcomeCounts = new RunOutcomeCounts(
            QueueCount: counts.QueueCount,
            TopicCount: counts.TopicCount,
            SubscriptionCount: counts.SubscriptionCount,
            RuleCount: counts.RuleCount,
            NewCount: batchCounts.NewCount,
            UpdatedCount: batchCounts.UpdatedCount,
            UnchangedCount: batchCounts.UnchangedCount,
            MissingCount: missingCount);
        await _runUpdater.RecordSuccessAsync(
            request.DiscoveryRunId, request.NamespaceId, outcomeCounts, completedUtc, durationMs, cancellationToken)
            .ConfigureAwait(false);
        _meter.RunsCompleted.Add(1, new KeyValuePair<string, object?>(DiscoveryMeter.TagStatus, "succeeded"));
        _meter.RunDuration.Record(durationMs);
        LogCompleted(request.DiscoveryRunId, request.NamespaceId, durationMs);
    }

    private async Task<(bool success, Exception? exception, DiscoveryFailureCategory category)>
        StreamScopeAsync(
            DiscoveredEntityType primaryScope,
            DiscoveryPhase phase,
            IAsyncEnumerable<DiscoveredEntity> source,
            ScopeCounts counts,
            DiscoveryWriteBatcher batcher,
            DiscoveryRunRequest request,
            DateTimeOffset startedUtc,
            CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var entity in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                var compositeKey = entity.CompositeKey;
                var entityId = PublishedEntityIdComputer.ComputeFromCompositeKey(compositeKey);
                var hash = AzureSourcedHash.Compute(entity.AzureSourced);
                string? parentId = entity.ParentCompositeKey is null
                    ? null
                    : PublishedEntityIdComputer.ComputeFromCompositeKey(entity.ParentCompositeKey);

                var upsert = new DiscoveryUpsert(
                    EntityId: entityId,
                    Environment: request.Environment,
                    EntityType: entity.EntityType,
                    NamespaceId: entity.NamespaceId,
                    Name: entity.Name,
                    CompositeKey: compositeKey,
                    ParentEntityId: parentId,
                    AzureSourced: entity.AzureSourced,
                    AzureSourcedHash: hash,
                    DiscoveryRunId: request.DiscoveryRunId,
                    DiscoveryRunStartedUtc: startedUtc,
                    DiscoveredBy: "discovery-worker");

                await batcher.EnqueueAsync(upsert, cancellationToken).ConfigureAwait(false);
                counts.Increment(entity.EntityType);
                _meter.EntitiesClassified.Add(1,
                    new KeyValuePair<string, object?>(DiscoveryMeter.TagEntityType, entity.EntityType.ToString()),
                    new KeyValuePair<string, object?>(DiscoveryMeter.TagClassificationOutcome, "observed"));
            }
            return (true, null, DiscoveryFailureCategory.Unknown);
        }
        catch (Exception ex)
        {
            return (false, ex, MapExceptionToCategory(ex));
        }
    }

    private async Task RecordFailureAsync(
        DiscoveryRunRequest request,
        DateTimeOffset startedUtc,
        Stopwatch stopwatch,
        Exception? exception,
        DiscoveryPhase phase,
        DiscoveryFailureCategory category,
        CancellationToken cancellationToken)
    {
        stopwatch.Stop();
        var completedUtc = _time.GetUtcNow();
        var failure = new RunFailureRecord(
            Category: category.ToString(),
            Message: FailureMessageSanitizer.Sanitize(exception?.Message),
            OccurredAtPhase: phase.ToString(),
            RetriesExhausted: null);
        try
        {
            await _runUpdater.RecordFailureAsync(
                request.DiscoveryRunId, request.NamespaceId, failure, completedUtc, (int)stopwatch.ElapsedMilliseconds, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception updateEx)
        {
            LogFailureWriteFailed(request.DiscoveryRunId, updateEx.Message);
        }
        _meter.RunsCompleted.Add(1,
            new KeyValuePair<string, object?>(DiscoveryMeter.TagStatus, "failed"),
            new KeyValuePair<string, object?>(DiscoveryMeter.TagFailureCategory, category.ToString()));
        _meter.RunDuration.Record(stopwatch.ElapsedMilliseconds);
        LogFailed(request.DiscoveryRunId, request.NamespaceId, phase.ToString(), category.ToString());
    }

    private static DiscoveryFailureCategory MapExceptionToCategory(Exception ex)
    {
        var typeName = ex.GetType().Name;
        if (typeName.Contains("Authentication", StringComparison.Ordinal)) return DiscoveryFailureCategory.Authn;
        if (typeName.Contains("Forbidden", StringComparison.Ordinal)) return DiscoveryFailureCategory.Authz;
        if (typeName.Contains("NotFound", StringComparison.Ordinal)) return DiscoveryFailureCategory.NotFound;
        if (typeName.Contains("Throttle", StringComparison.Ordinal)) return DiscoveryFailureCategory.Throttled;
        if (typeName.Contains("HttpRequest", StringComparison.Ordinal)) return DiscoveryFailureCategory.Transport;
        if (typeName.Contains("Timeout", StringComparison.Ordinal)) return DiscoveryFailureCategory.Transport;
        return DiscoveryFailureCategory.Unknown;
    }

    [LoggerMessage(EventId = 9111, Level = LogLevel.Information,
        Message = "Discovery run {RunId} completed for namespace {NamespaceId} in {DurationMs}ms.")]
    private partial void LogCompleted(string runId, string namespaceId, int durationMs);

    [LoggerMessage(EventId = 9112, Level = LogLevel.Warning,
        Message = "Discovery run {RunId} failed at {Phase} ({Category}) for namespace {NamespaceId}.")]
    private partial void LogFailed(string runId, string namespaceId, string phase, string category);

    [LoggerMessage(EventId = 9113, Level = LogLevel.Error,
        Message = "Failed to persist failure record for run {RunId}: {Reason}")]
    private partial void LogFailureWriteFailed(string runId, string reason);

    private sealed class ScopeCounts
    {
        public int QueueCount;
        public int TopicCount;
        public int SubscriptionCount;
        public int RuleCount;

        public void Increment(DiscoveredEntityType type)
        {
            switch (type)
            {
                case DiscoveredEntityType.Queue: Interlocked.Increment(ref QueueCount); break;
                case DiscoveredEntityType.Topic: Interlocked.Increment(ref TopicCount); break;
                case DiscoveredEntityType.Subscription: Interlocked.Increment(ref SubscriptionCount); break;
                case DiscoveredEntityType.Rule: Interlocked.Increment(ref RuleCount); break;
            }
        }
    }

    // We need a logger handle for the batcher inside the orchestrator —
    // synthesized via NullLogger because the batcher only needs the warning
    // sink for per-entity write failures.
    private static class sp_NullLogger<T>
    {
        public static Microsoft.Extensions.Logging.ILogger<T> Instance { get; } =
            Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;
    }
}

// Spec 009 — request shape passed from the function trigger to the
// orchestrator. The Function deserializes the Service Bus envelope, resolves
// the environment string (default "dev" for now — future spec will key it
// off the registered namespace), and delegates.
public sealed record DiscoveryRunRequest(
    string DiscoveryRunId,
    string NamespaceId,
    string Environment,
    string RequestedBy,
    string CorrelationId);

public sealed record NamespaceDiscoveryContext(
    string AzureSubscriptionId,
    string ResourceGroup,
    string NamespaceName,
    string Environment);

// Looks up the Azure coordinates of a registered namespace. The default
// implementation reads from the `registry-entities` container (same shape
// the API uses). Replaced in tests with a stub.
public interface INamespaceContextResolver
{
    Task<NamespaceDiscoveryContext> ResolveAsync(string namespaceId, CancellationToken cancellationToken);
}

// Failure-category + phase enums (worker-side; mirror the API-side ones).
public enum DiscoveryFailureCategory
{
    Authn, Authz, NotFound, Throttled, Transport, Internal, WorkerLost, Unknown,
}

public enum DiscoveryPhase
{
    LockAcquire, Enqueue, FetchQueues, FetchTopics, FetchSubscriptions, FetchRules, Persist, ResultWrite,
}
