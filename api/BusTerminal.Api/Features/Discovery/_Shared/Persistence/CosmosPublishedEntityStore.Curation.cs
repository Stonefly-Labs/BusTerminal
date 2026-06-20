using System.Net;
using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Api.Features.Discovery.Shared.Persistence;

// Spec 009 / Phase 6 / T109. Curation write paths for the published-entity
// store. Sits in a separate partial file so the discovery-time PATCH path
// (Phase 3) stays focused. Each method:
//   • PATCH on existing doc — preserves azureSourced.*, azureSourcedHash,
//     and any field NOT in the operations list (FR-016 invariant).
//   • ETag enforcement via PatchItemRequestOptions.IfMatchEtag.
//   • Returns the freshly-patched document + new Cosmos ETag.
public sealed partial class CosmosPublishedEntityStore
{
    public Task<PublishedEntityDetail> UpdateCuratedMetadataAsync(
        string entityId,
        string environment,
        CuratedMetadataPatch patch,
        string ifMatchEtag,
        string modifiedBy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchEtag);
        ArgumentNullException.ThrowIfNull(patch);

        var operations = new List<PatchOperation>();
        if (patch.Description.IsSet)
        {
            operations.Add(PatchOperation.Set("/description", patch.Description.Value));
        }
        if (patch.BusinessPurpose.IsSet)
        {
            operations.Add(PatchOperation.Set("/businessPurpose", patch.BusinessPurpose.Value));
        }
        if (patch.Tags.IsSet)
        {
            operations.Add(PatchOperation.Set("/tags", patch.Tags.Value ?? Array.Empty<string>()));
        }
        if (patch.DocumentationLinks.IsSet)
        {
            operations.Add(PatchOperation.Set("/documentationLinks",
                patch.DocumentationLinks.Value ?? Array.Empty<EntityDocumentationLink>()));
        }
        if (patch.ContactInformation.IsSet)
        {
            operations.Add(PatchOperation.Set("/contactInformation", patch.ContactInformation.Value));
        }
        if (patch.OperationalNotes.IsSet)
        {
            operations.Add(PatchOperation.Set("/operationalNotes", patch.OperationalNotes.Value));
        }

        var now = DateTimeOffset.UtcNow;
        operations.Add(PatchOperation.Set("/lastModifiedUtc", now));
        operations.Add(PatchOperation.Set("/lastModifiedBy", modifiedBy));

        return ExecutePatchAsync(entityId, environment, operations, ifMatchEtag, cancellationToken);
    }

    public Task<PublishedEntityDetail> SetLifecycleStatusAsync(
        string entityId,
        string environment,
        LifecycleStatus status,
        string ifMatchEtag,
        string modifiedBy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchEtag);

        var now = DateTimeOffset.UtcNow;
        var operations = new List<PatchOperation>
        {
            PatchOperation.Set("/lifecycleStatus", status.ToString()),
            PatchOperation.Set("/lifecycleStatusChangedUtc", now),
            PatchOperation.Set("/lastModifiedUtc", now),
            PatchOperation.Set("/lastModifiedBy", modifiedBy),
        };

        return ExecutePatchAsync(entityId, environment, operations, ifMatchEtag, cancellationToken);
    }

    public async Task<EntityServiceAssociationCreated> AddAssociationAsync(
        string entityId,
        string environment,
        EntityServiceAssociation association,
        string ifMatchEtag,
        string modifiedBy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchEtag);
        ArgumentNullException.ThrowIfNull(association);

        // Read current state to detect duplicate + compute denormalized
        // arrays. The IfMatch on the subsequent patch makes any race a 412.
        var current = await ReadDocumentAsync(entityId, environment, cancellationToken).ConfigureAwait(false);
        var existing = current.ServiceAssociations ?? Array.Empty<EntityServiceAssociation>();

        if (existing.Any(a => string.Equals(a.ServiceId, association.ServiceId, StringComparison.Ordinal) && a.Role == association.Role))
        {
            throw new DuplicateServiceAssociationException(entityId, association.ServiceId, association.Role.ToString());
        }

        var nextAssociations = existing.Append(association).ToArray();
        var detail = await ReplaceAssociationsAsync(entityId, environment, nextAssociations, ifMatchEtag, modifiedBy, cancellationToken).ConfigureAwait(false);
        return new EntityServiceAssociationCreated(association, detail);
    }

    public async Task<PublishedEntityDetail> RemoveAssociationAsync(
        string entityId,
        string environment,
        string associationId,
        string ifMatchEtag,
        string modifiedBy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(associationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchEtag);

        var current = await ReadDocumentAsync(entityId, environment, cancellationToken).ConfigureAwait(false);
        var existing = current.ServiceAssociations ?? Array.Empty<EntityServiceAssociation>();
        var without = existing.Where(a => !string.Equals(a.AssociationId, associationId, StringComparison.Ordinal)).ToArray();
        if (without.Length == existing.Count)
        {
            throw new ServiceAssociationNotFoundException(entityId, associationId);
        }

        return await ReplaceAssociationsAsync(entityId, environment, without, ifMatchEtag, modifiedBy, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PublishedEntityDocument> ReadDocumentAsync(string entityId, string environment, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<PublishedEntityDocument>(
                entityId,
                new PartitionKey(environment),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new PublishedEntityNotFoundException(entityId, environment);
        }
    }

    private Task<PublishedEntityDetail> ReplaceAssociationsAsync(
        string entityId,
        string environment,
        IReadOnlyList<EntityServiceAssociation> associations,
        string ifMatchEtag,
        string modifiedBy,
        CancellationToken cancellationToken)
    {
        var derivedServiceIds = associations.Select(a => a.ServiceId).Distinct(StringComparer.Ordinal).ToArray();
        var derivedRoles = associations.Select(a => a.Role).Distinct().Select(r => r.ToString()).ToArray();
        var now = DateTimeOffset.UtcNow;

        var operations = new List<PatchOperation>
        {
            PatchOperation.Set("/serviceAssociations", associations),
            PatchOperation.Set("/associatedServiceIds", derivedServiceIds),
            PatchOperation.Set("/associationRoles", derivedRoles),
            PatchOperation.Set("/lastModifiedUtc", now),
            PatchOperation.Set("/lastModifiedBy", modifiedBy),
        };

        return ExecutePatchAsync(entityId, environment, operations, ifMatchEtag, cancellationToken);
    }

    private async Task<PublishedEntityDetail> ExecutePatchAsync(
        string entityId,
        string environment,
        IReadOnlyList<PatchOperation> operations,
        string ifMatchEtag,
        CancellationToken cancellationToken)
    {
        var requestOptions = new PatchItemRequestOptions { IfMatchEtag = ifMatchEtag };

        try
        {
            var response = await _container.PatchItemAsync<PublishedEntityDocument>(
                entityId,
                new PartitionKey(environment),
                operations,
                requestOptions,
                cancellationToken).ConfigureAwait(false);

            // Reject legacy spec 006 docs (no azureSourced block) — they're
            // not published entities yet so this surface should never have
            // touched them. Defensive: keeps invariants stable for callers.
            if (response.Resource.AzureSourced.ValueKind == JsonValueKind.Undefined
                || response.Resource.AzureSourced.ValueKind == JsonValueKind.Null)
            {
                throw new PublishedEntityNotFoundException(entityId, environment);
            }

            var entity = MapToDomain(response.Resource, response.ETag);
            return new PublishedEntityDetail(entity, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new PublishedEntityNotFoundException(entityId, environment);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new PublishedEntityConcurrencyConflictException(entityId, ifMatchEtag, ex);
        }
    }
}
