using System.Collections.Generic;

namespace BusTerminal.Indexer.Discovery;

// Spec 009 / T048 (helper) + R-07/R-08. In-worker projection of an entity
// observed during a discovery scan. The `AzureSourced` field is a flat
// JSON-shaped map so the canonical-hash + classification logic operate on
// the same shape regardless of EntityType. The orchestrator stamps the
// `Discriminator` field (Queue|Topic|Subscription|Rule) which the API-side
// JsonPolymorphic shape uses for downstream deserialization.
public sealed record DiscoveredEntity(
    DiscoveredEntityType EntityType,
    string NamespaceId,
    string Name,
    string CompositeKey,
    string? ParentCompositeKey,
    IReadOnlyDictionary<string, object?> AzureSourced);

public enum DiscoveredEntityType
{
    Queue,
    Topic,
    Subscription,
    Rule,
}
