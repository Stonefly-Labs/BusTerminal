using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Relationships;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 004 / FR-020 + FR-025 + Q2. The canonical CRUD + soft-delete surface.
// `actor` and `sourceSystem` are required arguments (not pulled from ambient
// context) so call sites are forced to acknowledge audit responsibility and so
// system / sync-worker writes can override the request principal.
public interface ICanonicalResourceStore
{
    Task<Resource?> GetAsync(
        ResourceId id,
        string resourceTypeDiscriminator,
        bool includeDeleted,
        CancellationToken cancellationToken);

    IAsyncEnumerable<Resource> QueryAsync(
        ResourceQuery query,
        CancellationToken cancellationToken);

    Task<Resource> CreateAsync(
        Resource resource,
        PrincipalReference actor,
        string? sourceSystem,
        CancellationToken cancellationToken);

    Task<Resource> UpdateAsync(
        Resource resource,
        PrincipalReference actor,
        string? sourceSystem,
        CancellationToken cancellationToken);

    Task<Resource> SoftDeleteAsync(
        ResourceId id,
        string resourceTypeDiscriminator,
        ConcurrencyToken token,
        PrincipalReference actor,
        string? sourceSystem,
        CancellationToken cancellationToken);

    Task<Resource> RestoreAsync(
        ResourceId id,
        string resourceTypeDiscriminator,
        ConcurrencyToken token,
        PrincipalReference actor,
        string? sourceSystem,
        CancellationToken cancellationToken);

    // Relationship CRUD/query. Spec 004 / FR-008 / T104. Relationships live in
    // the same Cosmos container as Resources, under partition key
    // `resourceType` = "relationship".

    Task<Relationship> CreateRelationshipAsync(
        Relationship relationship,
        PrincipalReference actor,
        string? sourceSystem,
        CancellationToken cancellationToken);

    Task<Relationship?> GetRelationshipAsync(
        ResourceId id,
        bool includeDeleted,
        CancellationToken cancellationToken);

    IAsyncEnumerable<Relationship> QueryRelationshipsAsync(
        RelationshipQuery query,
        CancellationToken cancellationToken);
}
