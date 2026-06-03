namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-035 (env-scoped) + research §13 (pagination). Filter/page
// criteria for IRegistryEntityStore.ListAsync. `Environment` is REQUIRED —
// FR-035 mandates env-bound browse; cross-env discovery flows through the
// search endpoint (US2).
public sealed record RegistryEntityListQuery(
    string Environment,
    RegistryEntityType? EntityType = null,
    Guid? ParentId = null,
    RegistryEntityStatus? Status = null,
    int PageSize = 100,
    string? ContinuationToken = null);

// Returned page of entities + the opaque continuation token (null when the
// last page is reached). Match Cosmos `FeedResponse` pagination semantics.
public sealed record RegistryEntityPage(
    IReadOnlyList<RegistryEntity> Items,
    string? ContinuationToken);

// Per-entity-type breakdown returned by IRegistryEntityStore.CountChildrenAsync
// (consumed by ChildCountChecker T039 and the DELETE endpoint T079).
public sealed record ChildCount(
    int TotalChildren,
    IReadOnlyDictionary<RegistryEntityType, int> ChildrenByType);
