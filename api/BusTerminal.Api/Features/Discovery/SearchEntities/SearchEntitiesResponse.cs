using System.Text.Json.Serialization;
using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.SearchEntities;

// Spec 009 / T070 + contracts/openapi.yaml#PublishedEntitySummary.
// Wire-level DTO returned in each hit. Names match the OpenAPI shape so
// client Zod schemas (web/lib/discovery/schemas.ts:publishedEntitySummarySchema)
// validate without additional translation.
public sealed record PublishedEntitySummaryDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("entityType")] EntityType EntityType,
    [property: JsonPropertyName("namespaceId")] string NamespaceId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parentEntityId")] string? ParentEntityId,
    [property: JsonPropertyName("lifecycleStatus")] LifecycleStatus LifecycleStatus,
    [property: JsonPropertyName("lastSeenUtc")] DateTimeOffset? LastSeenUtc,
    [property: JsonPropertyName("associatedServiceIds")] IReadOnlyList<string> AssociatedServiceIds,
    [property: JsonPropertyName("associationRoles")] IReadOnlyList<EntityServiceRole> AssociationRoles,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags);

public sealed record SearchEntitiesResponseDto(
    [property: JsonPropertyName("items")] IReadOnlyList<PublishedEntitySummaryDto> Items,
    [property: JsonPropertyName("totalCount")] long TotalCount,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize);
