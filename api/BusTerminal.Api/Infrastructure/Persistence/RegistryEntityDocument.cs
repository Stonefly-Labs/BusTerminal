using System.Text.Json;
using System.Text.Json.Serialization;
using BusTerminal.Api.Features.Registry.Shared;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 006 / data-model.md §4.2. The persisted wire shape — what actually
// lives in `registry-entities`. The runtime types `RegistryEntity` (and the
// concrete sub-records `RegistryQueue`, etc.) carry the same fields but they
// are computed in-process from this document via the discriminator.
//
// Keeping this DTO separate from the runtime type lets the store deserialize
// without a polymorphic converter: read the DTO, switch on EntityType, hand
// back the concrete record. Concrete records ARE deserialized through the
// `RegistryEntity` base where STJ can pick up the abstract-record default
// constructor.
//
// Wire shape per data-model.md §4.2:
//   - canonical fields from §2
//   - `_isTombstone` (defaults to false) and `_tombstoneFor` (null on normal
//     documents) — used by the change-feed indexer (contracts/indexer-events.md).
//   - `_etag` is server-managed by Cosmos.
//   - `tagKeysLower` is the lowercase-key projection populated by the store
//     on every write (research §9 + data-model.md §6.1).
internal sealed record RegistryEntityDocument
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("entityType")] public required RegistryEntityType EntityType { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("fullyQualifiedName")] public string? FullyQualifiedName { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("tags")] public IReadOnlyList<RegistryTag>? Tags { get; init; }
    [JsonPropertyName("tagKeysLower")] public IReadOnlyList<string>? TagKeysLower { get; init; }
    [JsonPropertyName("owner")] public string? Owner { get; init; }
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("status")] public required RegistryEntityStatus Status { get; init; }
    [JsonPropertyName("createdAtUtc")] public required DateTimeOffset CreatedAtUtc { get; init; }
    [JsonPropertyName("updatedAtUtc")] public required DateTimeOffset UpdatedAtUtc { get; init; }
    [JsonPropertyName("source")] public required RegistrySource Source { get; init; }
    [JsonPropertyName("azureResourceId")] public string? AzureResourceId { get; init; }
    [JsonPropertyName("namespaceName")] public string? NamespaceName { get; init; }
    [JsonPropertyName("metadata")] public JsonElement? Metadata { get; init; }
    [JsonPropertyName("parentId")] public string? ParentId { get; init; }

    [JsonPropertyName("_isTombstone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsTombstone { get; init; }

    [JsonPropertyName("_tombstoneFor")] public string? TombstoneFor { get; init; }

    [JsonPropertyName("_etag")] public string? Etag { get; init; }

    public static RegistryEntityDocument FromEntity(RegistryEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new RegistryEntityDocument
        {
            Id = entity.Id.ToString("D"),
            EntityType = entity.EntityType,
            Name = entity.Name,
            FullyQualifiedName = entity.FullyQualifiedName,
            Description = entity.Description,
            Tags = entity.Tags.Count > 0 ? entity.Tags : null,
            TagKeysLower = entity.Tags.Count > 0
                ? entity.Tags.Select(t => t.TagKeyLower).Distinct(StringComparer.Ordinal).ToArray()
                : null,
            Owner = entity.Owner,
            Environment = entity.Environment,
            Status = entity.Status,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Source = entity.Source,
            AzureResourceId = entity.AzureResourceId,
            NamespaceName = entity.NamespaceName,
            Metadata = entity.Metadata,
            ParentId = entity.ParentId?.ToString("D"),
            Etag = entity.Etag,
        };
    }

    public RegistryEntity ToEntity()
    {
        var id = Guid.Parse(Id);
        var parentId = string.IsNullOrEmpty(ParentId) ? (Guid?)null : Guid.Parse(ParentId);
        var tags = Tags ?? Array.Empty<RegistryTag>();

        // Materialize the concrete sub-record matching the discriminator so
        // callers can pattern-match on type. The sub-record constructors all
        // funnel into the base record's positional fields, so the field set
        // returned here is identical to the persisted shape.
        return EntityType switch
        {
            RegistryEntityType.Namespace => new RegistryNamespace(
                id, Name, Environment, Status, CreatedAtUtc, UpdatedAtUtc, Source,
                FullyQualifiedName, Description, tags, Owner, AzureResourceId, Metadata, Etag),
            RegistryEntityType.Queue => new RegistryQueue(
                id, Name, Environment, Status, CreatedAtUtc, UpdatedAtUtc, Source,
                parentId ?? throw new InvalidOperationException("Queue document missing parentId."),
                FullyQualifiedName, Description, tags, Owner, AzureResourceId, NamespaceName, Metadata, Etag),
            RegistryEntityType.Topic => new RegistryTopic(
                id, Name, Environment, Status, CreatedAtUtc, UpdatedAtUtc, Source,
                parentId ?? throw new InvalidOperationException("Topic document missing parentId."),
                FullyQualifiedName, Description, tags, Owner, AzureResourceId, NamespaceName, Metadata, Etag),
            RegistryEntityType.Subscription => new RegistrySubscription(
                id, Name, Environment, Status, CreatedAtUtc, UpdatedAtUtc, Source,
                parentId ?? throw new InvalidOperationException("Subscription document missing parentId."),
                FullyQualifiedName, Description, tags, Owner, AzureResourceId, NamespaceName, Metadata, Etag),
            RegistryEntityType.Rule => new RegistryRule(
                id, Name, Environment, Status, CreatedAtUtc, UpdatedAtUtc, Source,
                parentId ?? throw new InvalidOperationException("Rule document missing parentId."),
                FullyQualifiedName, Description, tags, Owner, AzureResourceId, NamespaceName, Metadata, Etag),
            _ => throw new InvalidOperationException($"Unknown registry entity type '{EntityType}'."),
        };
    }
}

// Spec 006 / research §10. Persistence-internal tombstone marker. Written by
// CosmosRegistryEntityStore.DeleteAsync immediately before the point-delete so
// the change feed propagates the delete signal to AI Search.
internal sealed record RegistryTombstoneDocument
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("entityType")] public required RegistryEntityType EntityType { get; init; }

    [JsonPropertyName("_isTombstone")] public bool IsTombstone { get; init; } = true;
    [JsonPropertyName("_tombstoneFor")] public required string TombstoneFor { get; init; }

    [JsonPropertyName("ttl")] public int Ttl { get; init; }
}
