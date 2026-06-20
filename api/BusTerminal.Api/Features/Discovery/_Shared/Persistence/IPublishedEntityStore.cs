using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.Shared.Persistence;

// Spec 009 / T015. Persistence port for the published-entity view of the
// `registry-entities` Cosmos container. Distinct from IRegistryEntityStore
// (spec 006/008 / Guid-id entities) so the spec 009 surface stays focused
// on the discovery-relevant fields without churning the existing store's
// public contract.
public interface IPublishedEntityStore
{
    // FR-016 + R-08. Idempotent worker write. If the entity exists, only the
    // azureSourced.*, azureSourcedHash, lastSeenUtc, lastDiscoveryRunId, and
    // (when transitioning out of Missing) lifecycleStatus paths are touched —
    // curated fields and serviceAssociations remain untouched. If the entity
    // does not exist, a new document is created with sensible defaults for
    // every required field.
    //
    // `ifMatch` is honored when supplied (412 → CosmosException with the
    // PreconditionFailed status code surfaced as RegistryConcurrencyConflictException).
    Task UpsertAzureSourcedAsync(
        DiscoveredEntityUpsert upsert,
        string? ifMatch,
        CancellationToken cancellationToken);

    // Discovery-time classification read. Returns the minimal projection the
    // classifier needs (hash + last-seen) so RU cost stays low. Returns null
    // when the document does not yet exist (→ classifier treats as new).
    Task<PublishedEntityProjection?> GetForDiscoveryAsync(
        string entityId,
        string environment,
        CancellationToken cancellationToken);

    // Missing-sweep enumerator. Streams every Active entity in the namespace
    // whose lastSeenUtc predates the supplied cutoff. The worker transitions
    // each one to Missing via SetLifecycleStatusAsync (US4 / Phase 6 wiring
    // adds the matching write method).
    IAsyncEnumerable<PublishedEntityProjection> ListMissingCandidatesAsync(
        string namespaceId,
        string environment,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken);

    // Full-detail read for the GET /api/entities/{id} surface. Returns the
    // domain projection of the published entity document AND the Cosmos ETag
    // (echoed via the response's HTTP ETag header so clients can supply it
    // back on PATCH/POST/DELETE via `If-Match`). Returns null if the
    // document is not found in the supplied partition.
    Task<PublishedEntityDetail?> GetDetailAsync(
        string entityId,
        string environment,
        CancellationToken cancellationToken);

    // Spec 009 / T109. Curated metadata patch — the spec 006 fields
    // (description, businessPurpose, tags, documentationLinks,
    // contactInformation, operationalNotes). Only present fields on the
    // CuratedMetadataPatch are touched; azureSourced.* + serviceAssociations
    // + lifecycleStatus + all discovery-owned fields are NOT modified.
    //
    // Throws PublishedEntityNotFoundException (404) or
    // PublishedEntityConcurrencyConflictException (412 — stale ifMatchEtag).
    Task<PublishedEntityDetail> UpdateCuratedMetadataAsync(
        string entityId,
        string environment,
        CuratedMetadataPatch patch,
        string ifMatchEtag,
        string modifiedBy,
        CancellationToken cancellationToken);

    // Spec 009 / T109. Sets lifecycleStatus + lifecycleStatusChangedUtc.
    // Used by the Archive endpoint (US4) and reserved for the worker's
    // missing-sweep (transition Active → Missing). FR-015 sticky-archive is
    // enforced by the missing-sweep callsite (worker), not here — the store
    // is dumb.
    Task<PublishedEntityDetail> SetLifecycleStatusAsync(
        string entityId,
        string environment,
        LifecycleStatus status,
        string ifMatchEtag,
        string modifiedBy,
        CancellationToken cancellationToken);

    // Spec 009 / T109. Appends a new association to the entity. Computes
    // the denormalized `associatedServiceIds` + `associationRoles`
    // projections (data-model.md §1.1). Throws
    // DuplicateServiceAssociationException (409) if a (serviceId, role)
    // triple already exists. Concurrency is enforced via ifMatchEtag.
    Task<EntityServiceAssociationCreated> AddAssociationAsync(
        string entityId,
        string environment,
        EntityServiceAssociation association,
        string ifMatchEtag,
        string modifiedBy,
        CancellationToken cancellationToken);

    // Spec 009 / T109. Removes the association with the supplied id. Throws
    // ServiceAssociationNotFoundException (404) if no such id exists.
    Task<PublishedEntityDetail> RemoveAssociationAsync(
        string entityId,
        string environment,
        string associationId,
        string ifMatchEtag,
        string modifiedBy,
        CancellationToken cancellationToken);
}

// Spec 009 / T109. Curated-metadata patch carrier. Each field uses the
// OptionalValue<T> wrapper so the wire-level distinction between
// "field missing" (no change) and "field explicitly null" (clear) survives
// down to the store. The endpoint layer builds this from the JsonElement of
// the request body.
public sealed record CuratedMetadataPatch(
    OptionalValue<string?> Description,
    OptionalValue<string?> BusinessPurpose,
    OptionalValue<IReadOnlyList<string>?> Tags,
    OptionalValue<IReadOnlyList<EntityDocumentationLink>?> DocumentationLinks,
    OptionalValue<EntityContactInformation?> ContactInformation,
    OptionalValue<string?> OperationalNotes)
{
    public static CuratedMetadataPatch Empty { get; } = new(
        OptionalValue<string?>.Unset(),
        OptionalValue<string?>.Unset(),
        OptionalValue<IReadOnlyList<string>?>.Unset(),
        OptionalValue<IReadOnlyList<EntityDocumentationLink>?>.Unset(),
        OptionalValue<EntityContactInformation?>.Unset(),
        OptionalValue<string?>.Unset());

    public bool HasAnyField =>
        Description.IsSet
        || BusinessPurpose.IsSet
        || Tags.IsSet
        || DocumentationLinks.IsSet
        || ContactInformation.IsSet
        || OperationalNotes.IsSet;
}

public readonly record struct OptionalValue<T>(bool IsSet, T Value)
{
    public static OptionalValue<T> Unset() => new(false, default!);
    public static OptionalValue<T> Set(T value) => new(true, value);
}

// Spec 009 / T107 result. AddAssociationAsync needs to surface both the
// freshly-stored association (so the endpoint can return it as the 201
// body) and the entity-level ETag (so the client can use it on subsequent
// PATCH/DELETE). The detail carries the full entity for callers that want
// to render the up-to-date state.
public sealed record EntityServiceAssociationCreated(
    EntityServiceAssociation Association,
    PublishedEntityDetail Detail);

// Spec 009 / T071. Detail projection returned by GetDetailAsync. The
// PublishedEntity record carries every domain-level field; the ETag is
// surfaced separately so the endpoint can set the HTTP `ETag` header
// regardless of whether the JSON body includes it.
public sealed record PublishedEntityDetail(
    BusTerminal.Api.Features.Discovery.Shared.Domain.PublishedEntity Entity,
    string ETag);

// Spec 009 — input payload for the worker upsert. Encapsulates the fields
// that need to land on Cosmos in a single transaction; using a record keeps
// the call sites self-documenting and resilient to future field additions.
public sealed record DiscoveredEntityUpsert(
    string EntityId,
    string Environment,
    EntityType EntityType,
    string NamespaceId,
    string Name,
    string DisplayName,
    string CompositeKey,
    string? ParentEntityId,
    AzureSourcedEntity AzureSourced,
    string AzureSourcedHash,
    string DiscoveryRunId,
    DateTimeOffset DiscoveryRunStartedUtc,
    string DiscoveredBy);
