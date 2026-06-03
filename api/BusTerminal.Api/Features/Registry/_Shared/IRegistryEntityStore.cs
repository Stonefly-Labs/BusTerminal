namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / data-model.md §4.1 + research §10, §11, §13. Persistence port for
// the registry-entities Cosmos container. Implemented by
// CosmosRegistryEntityStore (T029); a separate in-memory test fake may be
// introduced for unit tests if needed (the integration-only test discipline of
// spec-004 carries over — most CosmosRegistryEntityStore tests run against the
// real dev Cosmos account via RegistryFixture).
public interface IRegistryEntityStore
{
    // Point-read; returns null on miss OR when the document is a tombstone
    // marker. Tombstones MUST NOT surface to API callers (research §10).
    Task<RegistryEntity?> GetAsync(
        Guid id,
        string environment,
        CancellationToken cancellationToken);

    // Env-scoped paginated list with optional filters. ContinuationToken
    // round-trips opaquely; client passes it back unchanged for the next page.
    // Excludes tombstone documents server-side.
    Task<RegistryEntityPage> ListAsync(
        RegistryEntityListQuery query,
        CancellationToken cancellationToken);

    // Create. The store stamps id/timestamps/etag and returns the persisted
    // shape including the server-assigned `_etag`.
    Task<RegistryEntity> CreateAsync(
        RegistryEntity entity,
        CancellationToken cancellationToken);

    // Replace with If-Match concurrency. Raises RegistryConcurrencyConflictException
    // on 412 PreconditionFailed (current state attached for the conflict mapper
    // to diff). Returns the persisted shape with the new `_etag`.
    Task<RegistryEntity> UpdateAsync(
        RegistryEntity entity,
        string ifMatchEtag,
        CancellationToken cancellationToken);

    // Tombstone-then-delete per research §10. The store writes the tombstone
    // marker first (so the change feed propagates the delete to AI Search) and
    // then point-deletes the original document. If-Match guards both steps.
    Task DeleteAsync(
        Guid id,
        string environment,
        string ifMatchEtag,
        CancellationToken cancellationToken);

    // Returns the children-by-type breakdown for FR-009 child-blocked-delete
    // validation. Partition-scoped query — research §11.
    Task<ChildCount> CountChildrenAsync(
        Guid parentId,
        string environment,
        CancellationToken cancellationToken);

    // Used by the parent-existence and duplicate-name rules (FR-008, FR-014).
    // Returns null if no entity exists with the given parent + entityType + name
    // in the given environment, or the entity itself if one exists.
    Task<RegistryEntity?> FindByParentAndNameAsync(
        Guid? parentId,
        RegistryEntityType entityType,
        string name,
        string environment,
        CancellationToken cancellationToken);

    // Used by ParentExistenceRule. Returns the parent entity if it exists in
    // the same environment AND has the expected entity type; null otherwise.
    Task<RegistryEntity?> FindParentAsync(
        Guid parentId,
        RegistryEntityType expectedParentType,
        string environment,
        CancellationToken cancellationToken);

    // Used by GET /api/registry/environments (T103c). Cross-partition distinct
    // query — bounded result size (operators configure a small env set per
    // tenant; FR-035 Assumptions).
    Task<IReadOnlyList<string>> ListDistinctEnvironmentsAsync(
        CancellationToken cancellationToken);
}
