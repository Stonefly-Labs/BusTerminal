using System.Text.Json;
using System.Text.Json.Serialization;
using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.Shared.Persistence;

// Spec 009 / data-model.md §1.1. Cosmos wire shape for the published entity
// record. Spec 009 stores its entities in the same `registry-entities`
// container that spec 006 / 008 use; the documents are distinguishable from
// the existing spec 006 Queue/Topic/Subscription/Rule documents by the
// presence of `lifecycleStatus` ∈ { Active, Missing, Archived } AND a
// `namespaceId` reference that the spec 006 records do not carry.
//
// Curated fields (description, businessPurpose, tags, documentationLinks,
// contactInformation, operationalNotes) live alongside the spec 009 fields
// on the same document. UpsertAzureSourcedAsync uses Cosmos PATCH to touch
// only the azureSourced.* + lastSeenUtc + lastDiscoveryRunId paths so
// curated fields and serviceAssociations are preserved verbatim (FR-016).
internal sealed record PublishedEntityDocument
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

    // Curated metadata block — spec 006/008 fields preserved across every
    // discovery upsert.
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("businessPurpose")] public string? BusinessPurpose { get; init; }
    [JsonPropertyName("tags")] public IReadOnlyList<string>? Tags { get; init; }
    [JsonPropertyName("documentationLinks")] public IReadOnlyList<EntityDocumentationLink>? DocumentationLinks { get; init; }
    [JsonPropertyName("contactInformation")] public EntityContactInformation? ContactInformation { get; init; }
    [JsonPropertyName("operationalNotes")] public string? OperationalNotes { get; init; }

    // Spec 009 fields.
    [JsonPropertyName("lifecycleStatus")] public required LifecycleStatus LifecycleStatus { get; init; }
    [JsonPropertyName("lifecycleStatusChangedUtc")] public required DateTimeOffset LifecycleStatusChangedUtc { get; init; }
    [JsonPropertyName("firstDiscoveredUtc")] public required DateTimeOffset FirstDiscoveredUtc { get; init; }
    [JsonPropertyName("lastSeenUtc")] public required DateTimeOffset LastSeenUtc { get; init; }
    [JsonPropertyName("lastDiscoveryRunId")] public required string LastDiscoveryRunId { get; init; }
    [JsonPropertyName("azureSourced")] public JsonElement AzureSourced { get; init; }
    [JsonPropertyName("azureSourcedHash")] public required string AzureSourcedHash { get; init; }
    [JsonPropertyName("serviceAssociations")] public IReadOnlyList<EntityServiceAssociation>? ServiceAssociations { get; init; }
    [JsonPropertyName("associatedServiceIds")] public IReadOnlyList<string>? AssociatedServiceIds { get; init; }
    [JsonPropertyName("associationRoles")] public IReadOnlyList<EntityServiceRole>? AssociationRoles { get; init; }

    // Audit + concurrency.
    [JsonPropertyName("createdUtc")] public DateTimeOffset CreatedUtc { get; init; }
    [JsonPropertyName("createdBy")] public string? CreatedBy { get; init; }
    [JsonPropertyName("lastModifiedUtc")] public DateTimeOffset LastModifiedUtc { get; init; }
    [JsonPropertyName("lastModifiedBy")] public string? LastModifiedBy { get; init; }
    [JsonPropertyName("_etag")] public string? Etag { get; init; }
}

// Spec 009 / persistence read projection used by the discovery worker's
// classifier (R-08) and the missing-sweep pass. Carries only the fields the
// worker needs to make its decisions — keeps RU cost predictable.
public sealed record PublishedEntityProjection(
    string Id,
    string Environment,
    string NamespaceId,
    EntityType EntityType,
    string Name,
    string CompositeKey,
    LifecycleStatus LifecycleStatus,
    string? AzureSourcedHash,
    DateTimeOffset? LastSeenUtc,
    DateTimeOffset FirstDiscoveredUtc,
    string ETag);
