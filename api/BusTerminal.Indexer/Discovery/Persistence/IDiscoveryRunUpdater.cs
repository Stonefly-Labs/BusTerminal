using System.Threading;

namespace BusTerminal.Indexer.Discovery.Persistence;

// Spec 009 / T053 indirection. The orchestrator updates the run's status +
// counts + failure detail through this thin port so the test fixture can
// observe transitions without standing up a Cosmos emulator. The API-side
// store (CosmosDiscoveryRunStore) writes the same document; this adapter
// replicates the PATCH ops the worker needs.
public interface IDiscoveryRunUpdater
{
    Task TransitionToInProgressAsync(string runId, string namespaceId, DateTimeOffset whenUtc, CancellationToken cancellationToken);

    Task RecordSuccessAsync(string runId, string namespaceId, RunOutcomeCounts counts, DateTimeOffset completedUtc, int durationMs, CancellationToken cancellationToken);

    Task RecordFailureAsync(string runId, string namespaceId, RunFailureRecord failure, DateTimeOffset completedUtc, int durationMs, CancellationToken cancellationToken);
}

public sealed record RunOutcomeCounts(
    int QueueCount,
    int TopicCount,
    int SubscriptionCount,
    int RuleCount,
    int NewCount,
    int UpdatedCount,
    int UnchangedCount,
    int MissingCount);

public sealed record RunFailureRecord(
    string Category,
    string Message,
    string OccurredAtPhase,
    int? RetriesExhausted);
