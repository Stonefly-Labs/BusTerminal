using System.Collections.Generic;
using System.Threading;

namespace BusTerminal.Indexer.Discovery.Providers;

// Spec 009 / T048 + Principle VI. Abstraction over the per-entity-type
// fetchers. The Azure implementation streams from ARM; future providers
// (Kafka, RabbitMQ, etc.) can plug in without touching the orchestrator,
// classifier, or persistence layers.
//
// Each `Stream*` method yields entities lazily so the orchestrator's
// Channel-backed write pipeline maintains constant memory regardless of
// namespace size (constitution: ≤ 4 GB worker memory budget).
public interface IEntityDiscoveryProvider
{
    IAsyncEnumerable<DiscoveredEntity> StreamQueuesAsync(
        EntityDiscoveryProviderContext context,
        CancellationToken cancellationToken);

    IAsyncEnumerable<DiscoveredEntity> StreamTopicsAsync(
        EntityDiscoveryProviderContext context,
        CancellationToken cancellationToken);

    // Yields subscriptions + rules. The orchestrator fans out by topic via
    // Parallel.ForEachAsync; the provider does NOT need to know about the
    // discovered topics — it walks the namespace top-down on its own.
    IAsyncEnumerable<DiscoveredEntity> StreamSubscriptionsAndRulesAsync(
        EntityDiscoveryProviderContext context,
        CancellationToken cancellationToken);
}

// Cross-call context the provider needs to scope its fetch.
public sealed record EntityDiscoveryProviderContext(
    string NamespaceId,
    string AzureSubscriptionId,
    string ResourceGroup,
    string NamespaceName);
