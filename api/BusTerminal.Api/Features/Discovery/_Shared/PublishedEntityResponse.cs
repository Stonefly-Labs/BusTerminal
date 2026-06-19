using System.Text.Json.Serialization;
using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.Shared;

// Spec 009 / contracts/openapi.yaml#PublishedEntity. Wire-shape DTO that
// flattens the C# record's nested `Registry: EntityRegistryMetadata` into
// the root-level fields the contract specifies. The domain `PublishedEntity`
// keeps its nested shape for in-memory ergonomics; this DTO is the
// serialization boundary for the GET/PATCH/POST endpoints.
public sealed record PublishedEntityResponse
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("schemaVersion")] public required string SchemaVersion { get; init; }
    [JsonPropertyName("entityType")] public required EntityType EntityType { get; init; }
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("namespaceId")] public required string NamespaceId { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("displayName")] public required string DisplayName { get; init; }
    [JsonPropertyName("compositeKey")] public required string CompositeKey { get; init; }
    [JsonPropertyName("parentEntityId")] public string? ParentEntityId { get; init; }

    // Curated fields — flattened from EntityRegistryMetadata.
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("businessPurpose")] public string? BusinessPurpose { get; init; }
    [JsonPropertyName("tags")] public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("documentationLinks")] public IReadOnlyList<EntityDocumentationLink> DocumentationLinks { get; init; } = Array.Empty<EntityDocumentationLink>();
    [JsonPropertyName("contactInformation")] public EntityContactInformation? ContactInformation { get; init; }
    [JsonPropertyName("operationalNotes")] public string? OperationalNotes { get; init; }

    // Spec 009 fields.
    [JsonPropertyName("lifecycleStatus")] public required LifecycleStatus LifecycleStatus { get; init; }
    [JsonPropertyName("lifecycleStatusChangedUtc")] public required DateTimeOffset LifecycleStatusChangedUtc { get; init; }
    [JsonPropertyName("firstDiscoveredUtc")] public required DateTimeOffset FirstDiscoveredUtc { get; init; }
    [JsonPropertyName("lastSeenUtc")] public required DateTimeOffset LastSeenUtc { get; init; }
    [JsonPropertyName("lastDiscoveryRunId")] public required string LastDiscoveryRunId { get; init; }
    [JsonPropertyName("azureSourced")] public required AzureSourcedEntity AzureSourced { get; init; }
    [JsonPropertyName("azureSourcedHash")] public required string AzureSourcedHash { get; init; }
    [JsonPropertyName("serviceAssociations")] public IReadOnlyList<EntityServiceAssociation> ServiceAssociations { get; init; } = Array.Empty<EntityServiceAssociation>();
    [JsonPropertyName("associatedServiceIds")] public IReadOnlyList<string> AssociatedServiceIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("associationRoles")] public IReadOnlyList<EntityServiceRole> AssociationRoles { get; init; } = Array.Empty<EntityServiceRole>();

    // Audit.
    [JsonPropertyName("createdUtc")] public DateTimeOffset CreatedUtc { get; init; }
    [JsonPropertyName("createdBy")] public string? CreatedBy { get; init; }
    [JsonPropertyName("lastModifiedUtc")] public DateTimeOffset LastModifiedUtc { get; init; }
    [JsonPropertyName("lastModifiedBy")] public string? LastModifiedBy { get; init; }
    [JsonPropertyName("etag")] public string? ETag { get; init; }

    public static PublishedEntityResponse From(PublishedEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new PublishedEntityResponse
        {
            Id = entity.Id,
            SchemaVersion = entity.SchemaVersion,
            EntityType = entity.EntityType,
            Environment = entity.Environment,
            NamespaceId = entity.NamespaceId,
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            CompositeKey = entity.CompositeKey,
            ParentEntityId = entity.ParentEntityId,
            Description = entity.Registry.Description,
            BusinessPurpose = entity.Registry.BusinessPurpose,
            Tags = entity.Registry.Tags,
            DocumentationLinks = entity.Registry.DocumentationLinks,
            ContactInformation = entity.Registry.ContactInformation,
            OperationalNotes = entity.Registry.OperationalNotes,
            LifecycleStatus = entity.LifecycleStatus,
            LifecycleStatusChangedUtc = entity.LifecycleStatusChangedUtc,
            FirstDiscoveredUtc = entity.FirstDiscoveredUtc,
            LastSeenUtc = entity.LastSeenUtc,
            LastDiscoveryRunId = entity.LastDiscoveryRunId,
            AzureSourced = entity.AzureSourced,
            AzureSourcedHash = entity.AzureSourcedHash,
            ServiceAssociations = entity.ServiceAssociations,
            AssociatedServiceIds = entity.AssociatedServiceIds,
            AssociationRoles = entity.AssociationRoles,
            CreatedUtc = entity.CreatedUtc,
            CreatedBy = entity.CreatedBy,
            LastModifiedUtc = entity.LastModifiedUtc,
            LastModifiedBy = entity.LastModifiedBy,
            ETag = entity.ETag,
        };
    }
}
