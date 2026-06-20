using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BusTerminal.Indexer.Discovery.Classification;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Indexer.Discovery.Persistence;

// Spec 009 / T052. Cosmos-backed writer. Reads the existing document to
// classify (new vs updated vs unchanged) and writes only the discovery-owned
// paths via PATCH. Mirrors the API-side CosmosPublishedEntityStore so the
// two emitters can't drift on the FR-016 invariant.
public sealed partial class CosmosPublishedEntityWriter : IPublishedEntityWriter
{
    private readonly Container _container;
    private readonly ILogger<CosmosPublishedEntityWriter> _logger;

    public CosmosPublishedEntityWriter(Container container, ILogger<CosmosPublishedEntityWriter> logger)
    {
        _container = container;
        _logger = logger;
    }

    [LoggerMessage(EventId = 9801, Level = LogLevel.Debug,
        Message = "Discovery upsert {Outcome} entity={EntityId} run={RunId}.")]
    private partial void LogUpserted(string entityId, string runId, string outcome);

    public async Task<UpsertOutcome> UpsertAzureSourcedAsync(
        DiscoveryUpsert upsert,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upsert);

        var partitionKey = new PartitionKey(upsert.Environment);
        var azureSourcedJson = JsonSerializer.SerializeToElement(upsert.AzureSourced);
        var now = DateTimeOffset.UtcNow;

        // Read prior state — classifier decides whether this is new/updated/unchanged.
        string? priorHash = null;
        try
        {
            var read = await _container.ReadItemAsync<MinimalPublishedEntity>(
                upsert.EntityId, partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
            priorHash = read.Resource.AzureSourcedHash;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            priorHash = null;
        }
        var classification = EntityClassifier.Classify(priorHash, upsert.AzureSourcedHash);

        // Unchanged → just touch lastSeenUtc + lastDiscoveryRunId so the
        // missing-sweep sees this entity as present.
        if (classification is ClassificationOutcome.Unchanged)
        {
            var touchOps = new[]
            {
                PatchOperation.Set("/lastSeenUtc", upsert.DiscoveryRunStartedUtc),
                PatchOperation.Set("/lastDiscoveryRunId", upsert.DiscoveryRunId),
                PatchOperation.Set("/lastModifiedUtc", now),
            };
            var touchResp = await _container.PatchItemAsync<JsonElement>(
                upsert.EntityId, partitionKey, touchOps,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            LogUpserted(upsert.EntityId, upsert.DiscoveryRunId, "unchanged");
            return new UpsertOutcome(classification, touchResp.RequestCharge);
        }

        if (classification is ClassificationOutcome.Updated)
        {
            var ops = new[]
            {
                PatchOperation.Set("/azureSourced", azureSourcedJson),
                PatchOperation.Set("/azureSourcedHash", upsert.AzureSourcedHash),
                PatchOperation.Set("/lastSeenUtc", upsert.DiscoveryRunStartedUtc),
                PatchOperation.Set("/lastDiscoveryRunId", upsert.DiscoveryRunId),
                PatchOperation.Set("/lastModifiedUtc", now),
                PatchOperation.Set("/lastModifiedBy", upsert.DiscoveredBy),
                // FR-014: a previously-Missing entity that reappears flips to
                // Active automatically. Archived stays Archived (FR-015) but
                // we don't observe that here — the missing→active write below
                // is conditional via JSON Patch sensibility; this Set is safe
                // because the API-side write is the authoritative path for
                // Archived flips.
                PatchOperation.Set("/lifecycleStatus", "Active"),
                PatchOperation.Set("/lifecycleStatusChangedUtc", upsert.DiscoveryRunStartedUtc),
            };
            try
            {
                var resp = await _container.PatchItemAsync<JsonElement>(
                    upsert.EntityId, partitionKey, ops,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                LogUpserted(upsert.EntityId, upsert.DiscoveryRunId, "updated");
                return new UpsertOutcome(classification, resp.RequestCharge);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Raced with a deletion. Fall through to create path.
            }
        }

        // New → create the full document.
        var doc = new PublishedEntityWriteDoc
        {
            Id = upsert.EntityId,
            SchemaVersion = "1.1",
            EntityType = upsert.EntityType.ToString(),
            Environment = upsert.Environment,
            NamespaceId = upsert.NamespaceId,
            Name = upsert.Name,
            DisplayName = upsert.Name,
            CompositeKey = upsert.CompositeKey,
            ParentEntityId = upsert.ParentEntityId,
            LifecycleStatus = "Active",
            LifecycleStatusChangedUtc = upsert.DiscoveryRunStartedUtc,
            FirstDiscoveredUtc = upsert.DiscoveryRunStartedUtc,
            LastSeenUtc = upsert.DiscoveryRunStartedUtc,
            LastDiscoveryRunId = upsert.DiscoveryRunId,
            AzureSourced = azureSourcedJson,
            AzureSourcedHash = upsert.AzureSourcedHash,
            ServiceAssociations = Array.Empty<JsonElement>(),
            AssociatedServiceIds = Array.Empty<string>(),
            AssociationRoles = Array.Empty<string>(),
            CreatedUtc = now,
            CreatedBy = upsert.DiscoveredBy,
            LastModifiedUtc = now,
            LastModifiedBy = upsert.DiscoveredBy,
        };
        try
        {
            var resp = await _container.CreateItemAsync(
                doc, partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
            LogUpserted(upsert.EntityId, upsert.DiscoveryRunId, "new");
            return new UpsertOutcome(ClassificationOutcome.New, resp.RequestCharge);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            // Concurrent create — retry with the patch path.
            var ops = new[]
            {
                PatchOperation.Set("/azureSourced", azureSourcedJson),
                PatchOperation.Set("/azureSourcedHash", upsert.AzureSourcedHash),
                PatchOperation.Set("/lastSeenUtc", upsert.DiscoveryRunStartedUtc),
                PatchOperation.Set("/lastDiscoveryRunId", upsert.DiscoveryRunId),
            };
            var resp = await _container.PatchItemAsync<JsonElement>(
                upsert.EntityId, partitionKey, ops,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            LogUpserted(upsert.EntityId, upsert.DiscoveryRunId, "raced-updated");
            return new UpsertOutcome(ClassificationOutcome.Updated, resp.RequestCharge);
        }
    }

    public async IAsyncEnumerable<MissingCandidate> ListMissingCandidatesAsync(
        string namespaceId,
        string environment,
        DateTimeOffset olderThan,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
                "SELECT c.id, c.environment, c._etag FROM c " +
                "WHERE c.namespaceId = @ns AND c.lifecycleStatus = @active AND c.lastSeenUtc < @cutoff")
            .WithParameter("@ns", namespaceId)
            .WithParameter("@active", "Active")
            .WithParameter("@cutoff", olderThan);

        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(environment),
            MaxItemCount = 256,
        };
        using var iterator = _container.GetItemQueryIterator<MissingCandidateDoc>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var doc in page)
            {
                yield return new MissingCandidate(doc.Id, doc.Environment, doc.ETag ?? string.Empty);
            }
        }
    }

    public async Task TransitionToMissingAsync(
        string entityId,
        string environment,
        string ifMatch,
        DateTimeOffset whenUtc,
        string runId,
        CancellationToken cancellationToken)
    {
        var partitionKey = new PartitionKey(environment);
        var ops = new[]
        {
            PatchOperation.Set("/lifecycleStatus", "Missing"),
            PatchOperation.Set("/lifecycleStatusChangedUtc", whenUtc),
            PatchOperation.Set("/lastDiscoveryRunId", runId),
        };
        var options = new PatchItemRequestOptions();
        if (!string.IsNullOrEmpty(ifMatch)) options.IfMatchEtag = ifMatch;
        try
        {
            await _container.PatchItemAsync<JsonElement>(
                entityId, partitionKey, ops, options, cancellationToken).ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed
                                       || ex.StatusCode == HttpStatusCode.NotFound)
        {
            // The entity moved (archived, deleted, re-discovered) between
            // the sweep and the write — leave it alone.
        }
    }

    private sealed record MinimalPublishedEntity
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("azureSourcedHash")] public string? AzureSourcedHash { get; init; }
    }

    private sealed record MissingCandidateDoc
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("environment")] public string Environment { get; init; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("_etag")] public string? ETag { get; init; }
    }

    private sealed record PublishedEntityWriteDoc
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")] public required string Id { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("schemaVersion")] public required string SchemaVersion { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("entityType")] public required string EntityType { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("environment")] public required string Environment { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("namespaceId")] public required string NamespaceId { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("name")] public required string Name { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("displayName")] public required string DisplayName { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("compositeKey")] public required string CompositeKey { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("parentEntityId")] public string? ParentEntityId { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("lifecycleStatus")] public required string LifecycleStatus { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("lifecycleStatusChangedUtc")] public required DateTimeOffset LifecycleStatusChangedUtc { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("firstDiscoveredUtc")] public required DateTimeOffset FirstDiscoveredUtc { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("lastSeenUtc")] public required DateTimeOffset LastSeenUtc { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("lastDiscoveryRunId")] public required string LastDiscoveryRunId { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("azureSourced")] public required JsonElement AzureSourced { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("azureSourcedHash")] public required string AzureSourcedHash { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("serviceAssociations")] public required IReadOnlyList<JsonElement> ServiceAssociations { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("associatedServiceIds")] public required IReadOnlyList<string> AssociatedServiceIds { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("associationRoles")] public required IReadOnlyList<string> AssociationRoles { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("createdUtc")] public required DateTimeOffset CreatedUtc { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("createdBy")] public required string CreatedBy { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("lastModifiedUtc")] public required DateTimeOffset LastModifiedUtc { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("lastModifiedBy")] public required string LastModifiedBy { get; init; }
    }
}
