using Microsoft.Azure.Cosmos;

namespace BusTerminal.Indexer.Discovery.Persistence;

// Spec 009 / T053. Worker-side adapter writing the discovery-runs document
// via raw PATCH. Mirrors CosmosDiscoveryRunStore.UpdateStatusAsync on the
// API side; duplication avoids cross-project references for the Functions
// worker.
public sealed class CosmosDiscoveryRunUpdater : IDiscoveryRunUpdater
{
    private readonly Container _container;

    public CosmosDiscoveryRunUpdater(Container container)
    {
        _container = container;
    }

    public async Task TransitionToInProgressAsync(string runId, string namespaceId, DateTimeOffset whenUtc, CancellationToken cancellationToken)
    {
        var ops = new[]
        {
            PatchOperation.Set("/status", "InProgress"),
            PatchOperation.Set("/startedUtc", whenUtc),
        };
        await _container.PatchItemAsync<object>(
            runId, new PartitionKey(namespaceId), ops, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordSuccessAsync(string runId, string namespaceId, RunOutcomeCounts counts, DateTimeOffset completedUtc, int durationMs, CancellationToken cancellationToken)
    {
        var ops = new[]
        {
            PatchOperation.Set("/status", "Succeeded"),
            PatchOperation.Set("/completedUtc", completedUtc),
            PatchOperation.Set("/durationMs", durationMs),
            PatchOperation.Set("/queueCount", counts.QueueCount),
            PatchOperation.Set("/topicCount", counts.TopicCount),
            PatchOperation.Set("/subscriptionCount", counts.SubscriptionCount),
            PatchOperation.Set("/ruleCount", counts.RuleCount),
            PatchOperation.Set("/newCount", counts.NewCount),
            PatchOperation.Set("/updatedCount", counts.UpdatedCount),
            PatchOperation.Set("/unchangedCount", counts.UnchangedCount),
            PatchOperation.Set("/missingCount", counts.MissingCount),
        };
        await _container.PatchItemAsync<object>(
            runId, new PartitionKey(namespaceId), ops, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordFailureAsync(string runId, string namespaceId, RunFailureRecord failure, DateTimeOffset completedUtc, int durationMs, CancellationToken cancellationToken)
    {
        var failureDoc = new
        {
            category = failure.Category,
            message = failure.Message,
            occurredAtPhase = failure.OccurredAtPhase,
            retriesExhausted = failure.RetriesExhausted,
        };
        var ops = new[]
        {
            PatchOperation.Set("/status", "Failed"),
            PatchOperation.Set("/completedUtc", completedUtc),
            PatchOperation.Set("/durationMs", durationMs),
            PatchOperation.Set("/failure", failureDoc),
        };
        await _container.PatchItemAsync<object>(
            runId, new PartitionKey(namespaceId), ops, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
