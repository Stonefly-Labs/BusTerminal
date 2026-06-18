using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Features.Discovery.Shared.Persistence;

// Spec 009 / T015 + R-08. Cosmos-backed IPublishedEntityStore. Operates on
// the same `registry-entities` container that CosmosRegistryEntityStore
// targets. Spec 009 documents are distinguishable by `lifecycleStatus`
// (Active|Missing|Archived) and `namespaceId` being populated — fields the
// spec 006 Queue/Topic/Subscription/Rule docs do not carry.
//
// FR-016 invariant: UpsertAzureSourcedAsync MUST NOT clobber curated fields.
// Implementation uses Cosmos PATCH (`PatchItemAsync`) for existing documents,
// targeting only the discovery-owned paths. New documents are CREATEd with
// empty curated metadata + empty serviceAssociations.
public sealed partial class CosmosPublishedEntityStore : IPublishedEntityStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosPublishedEntityStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = AzureSourcedJsonConfig.CreateOptions();

    public CosmosPublishedEntityStore(
        CosmosClient client,
        IOptions<CosmosRegistryOptions> options,
        ILogger<CosmosPublishedEntityStore> logger)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.Database, opts.EntitiesContainer);
        _logger = logger;
    }

    [LoggerMessage(EventId = 9101, Level = LogLevel.Debug, Message = "PublishedEntity upsert created new document {EntityId} in env {Environment}.")]
    private partial void LogUpsertCreated(string entityId, string environment);

    [LoggerMessage(EventId = 9102, Level = LogLevel.Debug, Message = "PublishedEntity upsert patched {EntityId} (env {Environment}, run {DiscoveryRunId}).")]
    private partial void LogUpsertPatched(string entityId, string environment, string discoveryRunId);

    public async Task UpsertAzureSourcedAsync(
        DiscoveredEntityUpsert upsert,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upsert);
        ArgumentException.ThrowIfNullOrWhiteSpace(upsert.EntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(upsert.Environment);

        var partitionKey = new PartitionKey(upsert.Environment);
        var azureSourcedJson = JsonSerializer.SerializeToElement<AzureSourcedEntity>(upsert.AzureSourced, JsonOptions);
        var now = DateTimeOffset.UtcNow;

        // PATCH path — only the spec 009 azureSourced.*+ lastSeen + run +
        // lifecycle (when transitioning out of Missing). Curated fields and
        // serviceAssociations are intentionally NOT in the operations list.
        var patchOperations = new List<PatchOperation>
        {
            PatchOperation.Set("/azureSourced", azureSourcedJson),
            PatchOperation.Set("/azureSourcedHash", upsert.AzureSourcedHash),
            PatchOperation.Set("/lastSeenUtc", upsert.DiscoveryRunStartedUtc),
            PatchOperation.Set("/lastDiscoveryRunId", upsert.DiscoveryRunId),
            PatchOperation.Set("/lastModifiedUtc", now),
            PatchOperation.Set("/lastModifiedBy", upsert.DiscoveredBy),
        };

        var patchRequestOptions = new PatchItemRequestOptions();
        if (!string.IsNullOrEmpty(ifMatch))
        {
            patchRequestOptions.IfMatchEtag = ifMatch;
        }

        try
        {
            await _container.PatchItemAsync<PublishedEntityDocument>(
                upsert.EntityId,
                partitionKey,
                patchOperations,
                patchRequestOptions,
                cancellationToken).ConfigureAwait(false);
            LogUpsertPatched(upsert.EntityId, upsert.Environment, upsert.DiscoveryRunId);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // First sighting — create the full document.
            var fresh = new PublishedEntityDocument
            {
                Id = upsert.EntityId,
                SchemaVersion = PublishedEntity.CurrentSchemaVersion,
                EntityType = upsert.EntityType,
                Environment = upsert.Environment,
                NamespaceId = upsert.NamespaceId,
                Name = upsert.Name,
                DisplayName = upsert.DisplayName,
                CompositeKey = upsert.CompositeKey,
                ParentEntityId = upsert.ParentEntityId,
                LifecycleStatus = LifecycleStatus.Active,
                LifecycleStatusChangedUtc = upsert.DiscoveryRunStartedUtc,
                FirstDiscoveredUtc = upsert.DiscoveryRunStartedUtc,
                LastSeenUtc = upsert.DiscoveryRunStartedUtc,
                LastDiscoveryRunId = upsert.DiscoveryRunId,
                AzureSourced = azureSourcedJson,
                AzureSourcedHash = upsert.AzureSourcedHash,
                ServiceAssociations = Array.Empty<EntityServiceAssociation>(),
                AssociatedServiceIds = Array.Empty<string>(),
                AssociationRoles = Array.Empty<EntityServiceRole>(),
                CreatedUtc = now,
                CreatedBy = upsert.DiscoveredBy,
                LastModifiedUtc = now,
                LastModifiedBy = upsert.DiscoveredBy,
            };

            await _container.CreateItemAsync(
                fresh,
                partitionKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            LogUpsertCreated(upsert.EntityId, upsert.Environment);
        }
    }

    public async Task<PublishedEntityProjection?> GetForDiscoveryAsync(
        string entityId,
        string environment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);

        try
        {
            var response = await _container.ReadItemAsync<PublishedEntityDocument>(
                entityId,
                new PartitionKey(environment),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var doc = response.Resource;
            return new PublishedEntityProjection(
                Id: doc.Id,
                Environment: doc.Environment,
                NamespaceId: doc.NamespaceId,
                EntityType: doc.EntityType,
                Name: doc.Name,
                CompositeKey: doc.CompositeKey,
                LifecycleStatus: doc.LifecycleStatus,
                AzureSourcedHash: doc.AzureSourcedHash,
                LastSeenUtc: doc.LastSeenUtc,
                FirstDiscoveredUtc: doc.FirstDiscoveredUtc,
                ETag: response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<PublishedEntityDetail?> GetDetailAsync(
        string entityId,
        string environment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);

        try
        {
            var response = await _container.ReadItemAsync<PublishedEntityDocument>(
                entityId,
                new PartitionKey(environment),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var doc = response.Resource;

            // Spec 009 / GET /api/entities/{id} surfaces published entities
            // only — i.e. documents that have been through at least one
            // discovery run. Legacy spec 006 docs (no azureSourced block)
            // are not part of this surface and read as 404.
            if (doc.AzureSourced.ValueKind == JsonValueKind.Undefined
                || doc.AzureSourced.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var entity = MapToDomain(doc, response.ETag);
            return new PublishedEntityDetail(entity, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static PublishedEntity MapToDomain(PublishedEntityDocument doc, string etag)
    {
        var azureSourced = JsonSerializer.Deserialize<AzureSourcedEntity>(doc.AzureSourced, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize azureSourced for {doc.Id}.");

        var registry = new EntityRegistryMetadata(
            Description: doc.Description,
            BusinessPurpose: doc.BusinessPurpose,
            Tags: doc.Tags ?? Array.Empty<string>(),
            DocumentationLinks: doc.DocumentationLinks ?? Array.Empty<EntityDocumentationLink>(),
            ContactInformation: doc.ContactInformation,
            OperationalNotes: doc.OperationalNotes);

        return new PublishedEntity(
            Id: doc.Id,
            SchemaVersion: doc.SchemaVersion,
            EntityType: doc.EntityType,
            Environment: doc.Environment,
            NamespaceId: doc.NamespaceId,
            Name: doc.Name,
            DisplayName: doc.DisplayName,
            CompositeKey: doc.CompositeKey,
            ParentEntityId: doc.ParentEntityId,
            Registry: registry,
            LifecycleStatus: doc.LifecycleStatus,
            LifecycleStatusChangedUtc: doc.LifecycleStatusChangedUtc,
            FirstDiscoveredUtc: doc.FirstDiscoveredUtc,
            LastSeenUtc: doc.LastSeenUtc,
            LastDiscoveryRunId: doc.LastDiscoveryRunId,
            AzureSourced: azureSourced,
            AzureSourcedHash: doc.AzureSourcedHash,
            ServiceAssociations: doc.ServiceAssociations ?? Array.Empty<EntityServiceAssociation>(),
            CreatedUtc: doc.CreatedUtc,
            CreatedBy: doc.CreatedBy ?? string.Empty,
            LastModifiedUtc: doc.LastModifiedUtc,
            LastModifiedBy: doc.LastModifiedBy ?? string.Empty,
            ETag: etag);
    }

    public async IAsyncEnumerable<PublishedEntityProjection> ListMissingCandidatesAsync(
        string namespaceId,
        string environment,
        DateTimeOffset olderThan,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);

        var query = new QueryDefinition(
                "SELECT c.id, c.environment, c.namespaceId, c.entityType, c.name, c.compositeKey, " +
                "       c.lifecycleStatus, c.azureSourcedHash, c.lastSeenUtc, c.firstDiscoveredUtc, c._etag " +
                "FROM c " +
                "WHERE c.namespaceId = @ns AND c.lifecycleStatus = @active AND c.lastSeenUtc < @cutoff")
            .WithParameter("@ns", namespaceId)
            .WithParameter("@active", LifecycleStatus.Active.ToString())
            .WithParameter("@cutoff", olderThan);

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(environment),
            MaxItemCount = 256,
        };

        using var iterator = _container.GetItemQueryIterator<PublishedEntityProjectionDocument>(query, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var doc in page)
            {
                yield return new PublishedEntityProjection(
                    Id: doc.Id,
                    Environment: doc.Environment,
                    NamespaceId: doc.NamespaceId,
                    EntityType: doc.EntityType,
                    Name: doc.Name,
                    CompositeKey: doc.CompositeKey,
                    LifecycleStatus: doc.LifecycleStatus,
                    AzureSourcedHash: doc.AzureSourcedHash,
                    LastSeenUtc: doc.LastSeenUtc,
                    FirstDiscoveredUtc: doc.FirstDiscoveredUtc,
                    ETag: doc.Etag ?? string.Empty);
            }
        }
    }

    // Projection target for the missing-sweep query — minimal field set so
    // Cosmos returns only the bytes the worker needs.
    private sealed record PublishedEntityProjectionDocument
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")] public required string Id { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("environment")] public required string Environment { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("namespaceId")] public required string NamespaceId { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("entityType")] public required EntityType EntityType { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("name")] public required string Name { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("compositeKey")] public required string CompositeKey { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("lifecycleStatus")] public required LifecycleStatus LifecycleStatus { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("azureSourcedHash")] public string? AzureSourcedHash { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("lastSeenUtc")] public DateTimeOffset? LastSeenUtc { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("firstDiscoveredUtc")] public DateTimeOffset FirstDiscoveredUtc { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("_etag")] public string? Etag { get; init; }
    }
}
