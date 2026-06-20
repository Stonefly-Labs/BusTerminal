using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.Shared.Search;

// Spec 009 / T070. Search adapter port for the published-entity catalog.
// Distinct from ISearchClient (spec 006 — Guid-id RegistryEntity) because
// PublishedEntity has a different identity scheme (`pe_<base32>`), a
// different entity-type enum (Queue|Topic|Subscription|Rule only), and the
// spec 009 filters (lifecycleStatus, associatedServiceId, associationRole)
// that the spec 006 surface does not carry.
//
// Both adapters target the same `registry-entities-v1` AI Search index;
// per data-model.md §2.1 the spec 009 fields were added additively so
// neither surface sees the other's documents through a filter that doesn't
// apply.
public interface IPublishedEntitySearchClient
{
    Task<PublishedEntitySearchResults> SearchAsync(
        PublishedEntitySearchRequest request,
        CancellationToken cancellationToken);
}

// Spec 009 / contracts/openapi.yaml#searchEntities. Per-field nullability
// matches the wire shape.
public sealed record PublishedEntitySearchRequest(
    string? Query = null,
    IReadOnlyList<EntityType>? EntityTypeFilters = null,
    string? NamespaceIdFilter = null,
    string? AssociatedServiceIdFilter = null,
    IReadOnlyList<EntityServiceRole>? AssociationRoleFilters = null,
    IReadOnlyList<string>? TagFilters = null,
    IReadOnlyList<LifecycleStatus>? LifecycleStatusFilters = null,
    PublishedEntitySearchSort Sort = PublishedEntitySearchSort.NameAsc,
    int Skip = 0,
    int Top = 25);

public enum PublishedEntitySearchSort
{
    NameAsc,
    NameDesc,
    LastSeenAsc,
    LastSeenDesc,
}

public sealed record PublishedEntitySearchResults(
    IReadOnlyList<PublishedEntitySearchHit> Hits,
    long TotalCount);

public sealed record PublishedEntitySearchHit(
    string Id,
    EntityType EntityType,
    string NamespaceId,
    string Name,
    string? ParentEntityId,
    LifecycleStatus LifecycleStatus,
    DateTimeOffset? LastSeenUtc,
    string? Environment,
    IReadOnlyList<string> AssociatedServiceIds,
    IReadOnlyList<EntityServiceRole> AssociationRoles,
    IReadOnlyList<string> Tags);
