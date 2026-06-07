using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Indexer.Indexing;

// Spec 006 / contracts/indexer-events.md §2. Wire-shape for what the Cosmos
// change-feed trigger delivers to the indexer. Mirrors the canonical
// registry-entity.schema.json plus the registry-internal tombstone markers.
//
// The indexer redeclares these types (rather than referencing BusTerminal.Api)
// so the worker image stays narrow and the API-layer dependencies don't bleed
// into the indexer's deploy artifact (Modular Monolith Principle —
// event-driven extraction is the boundary).
public sealed record RegistryEntityChangeFeedItem
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("entityType")] public string? EntityType { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("fullyQualifiedName")] public string? FullyQualifiedName { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("tags")] public IReadOnlyList<RegistryTagItem>? Tags { get; init; }
    [JsonPropertyName("owner")] public string? Owner { get; init; }
    [JsonPropertyName("environment")] public string? Environment { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("createdAtUtc")] public DateTimeOffset? CreatedAtUtc { get; init; }
    [JsonPropertyName("updatedAtUtc")] public DateTimeOffset? UpdatedAtUtc { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("azureResourceId")] public string? AzureResourceId { get; init; }
    [JsonPropertyName("namespaceName")] public string? NamespaceName { get; init; }
    [JsonPropertyName("metadata")] public JsonElement? Metadata { get; init; }
    [JsonPropertyName("parentId")] public string? ParentId { get; init; }

    [JsonPropertyName("_isTombstone")] public bool IsTombstone { get; init; }
    [JsonPropertyName("_tombstoneFor")] public string? TombstoneFor { get; init; }
    [JsonPropertyName("_etag")] public string? Etag { get; init; }
}

public sealed record RegistryTagItem
{
    [JsonPropertyName("key")] public string Key { get; init; } = string.Empty;
    [JsonPropertyName("value")] public string Value { get; init; } = string.Empty;
}
