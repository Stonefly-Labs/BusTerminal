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
}

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
