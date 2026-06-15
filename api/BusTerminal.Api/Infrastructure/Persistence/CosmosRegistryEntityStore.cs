using System.Net;
using BusTerminal.Api.Features.Namespaces.Inventory;
using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 006 / data-model.md §4.1 + research §10, §11, §13. CRUD + paginated
// list + child-count against the `registry-entities` container (PK
// `/environment`). ETag-based optimistic concurrency on PUT/DELETE; the 412
// path raises RegistryConcurrencyConflictException so the endpoint layer can
// run ConcurrencyConflictMapper (T038) to shape the 409 ConflictResponse.
public sealed partial class CosmosRegistryEntityStore : IRegistryEntityStore
{
    private readonly Container _container;
    private readonly int _tombstoneTtlSeconds;
    private readonly ILogger<CosmosRegistryEntityStore> _logger;

    public CosmosRegistryEntityStore(
        CosmosClient client,
        IOptions<CosmosRegistryOptions> options,
        ILogger<CosmosRegistryEntityStore> logger)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.Database, opts.EntitiesContainer);
        _tombstoneTtlSeconds = opts.TombstoneTtlSeconds;
        _logger = logger;
    }

    [LoggerMessage(EventId = 6201, Level = LogLevel.Warning, Message = "Tombstone write for entity {EntityId} succeeded but the follow-up delete failed; the indexer will receive a spurious delete signal. AI Search may briefly miss the entity until the next index refresh.")]
    private partial void LogTombstoneWithoutDelete(Exception exception, Guid entityId);

    public async Task<RegistryEntity?> GetAsync(
        Guid id,
        string environment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);

        try
        {
            var response = await _container.ReadItemAsync<RegistryEntityDocument>(
                id.ToString("D"),
                new PartitionKey(environment),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var doc = response.Resource;
            // Tombstones MUST NOT surface to API callers (research §10 +
            // T076 GET endpoint contract). They live in the container only
            // long enough for the change feed to deliver them to the indexer.
            if (doc is null || doc.IsTombstone)
            {
                return null;
            }

            return doc with { Etag = response.ETag } switch { var d => d.ToEntity() };
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<RegistryEntityPage> ListAsync(
        RegistryEntityListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrEmpty(query.Environment);

        // FR-035: env-scoped browse. The query is partition-bound by
        // environment so RU cost is predictable.
        // Tombstone exclusion (T077 spec text): `WHERE NOT IS_DEFINED(c._isTombstone) OR c._isTombstone = false`.
        var sql = new System.Text.StringBuilder("SELECT * FROM c WHERE (NOT IS_DEFINED(c._isTombstone) OR c._isTombstone = false)");
        var parameters = new List<(string Name, object Value)>();

        if (query.EntityType.HasValue)
        {
            sql.Append(" AND c.entityType = @entityType");
            parameters.Add(("@entityType", query.EntityType.Value.ToString()));
        }
        if (query.ParentId.HasValue)
        {
            sql.Append(" AND c.parentId = @parentId");
            parameters.Add(("@parentId", query.ParentId.Value.ToString("D")));
        }
        if (query.Status.HasValue)
        {
            sql.Append(" AND c.status = @status");
            parameters.Add(("@status", query.Status.Value.ToString()));
        }

        sql.Append(" ORDER BY c.updatedAtUtc DESC");

        var definition = new QueryDefinition(sql.ToString());
        foreach (var (name, value) in parameters)
        {
            definition = definition.WithParameter(name, value);
        }

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(query.Environment),
            MaxItemCount = query.PageSize,
        };

        using var iterator = _container.GetItemQueryIterator<RegistryEntityDocument>(
            definition,
            continuationToken: query.ContinuationToken,
            requestOptions: requestOptions);

        if (!iterator.HasMoreResults)
        {
            return new RegistryEntityPage(Array.Empty<RegistryEntity>(), ContinuationToken: null);
        }

        var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
        var items = page.Select(d => d.ToEntity()).ToArray();
        return new RegistryEntityPage(items, page.ContinuationToken);
    }

    public async Task<RegistryEntity> CreateAsync(
        RegistryEntity entity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var doc = RegistryEntityDocument.FromEntity(entity);
        var response = await _container.CreateItemAsync(
            doc,
            new PartitionKey(doc.Environment),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return (response.Resource with { Etag = response.ETag }).ToEntity();
    }

    public async Task<RegistryEntity> UpdateAsync(
        RegistryEntity entity,
        string ifMatchEtag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrEmpty(ifMatchEtag);

        var doc = RegistryEntityDocument.FromEntity(entity);

        try
        {
            var response = await _container.ReplaceItemAsync(
                doc,
                doc.Id,
                new PartitionKey(doc.Environment),
                requestOptions: new ItemRequestOptions { IfMatchEtag = ifMatchEtag },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return (response.Resource with { Etag = response.ETag }).ToEntity();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            // The 412 path is the conflict response. The endpoint layer reads
            // the current entity separately and calls ConcurrencyConflictMapper
            // (T038) to produce the wire-shape ConflictResponse.
            throw new RegistryConcurrencyConflictException(
                entity.Id, ifMatchEtag, currentEtag: null, innerException: ex);
        }
    }

    public async Task DeleteAsync(
        Guid id,
        string environment,
        string ifMatchEtag,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(ifMatchEtag);

        // research §10 — write the tombstone first so the change feed
        // delivers the delete signal to the indexer, then point-delete the
        // original. If the original delete races a concurrent update (412),
        // the tombstone is still TTL-bounded and self-expires.
        var tombstone = new RegistryTombstoneDocument
        {
            Id = Guid.NewGuid().ToString("D"),
            Environment = environment,
            EntityType = await ReadEntityTypeForTombstoneAsync(id, environment, cancellationToken).ConfigureAwait(false),
            TombstoneFor = id.ToString("D"),
            Ttl = _tombstoneTtlSeconds,
        };

        await _container.CreateItemAsync(
            tombstone,
            new PartitionKey(environment),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        try
        {
            await _container.DeleteItemAsync<RegistryEntityDocument>(
                id.ToString("D"),
                new PartitionKey(environment),
                requestOptions: new ItemRequestOptions { IfMatchEtag = ifMatchEtag },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            // The original has moved on. Log + bubble — the tombstone TTL
            // cleans itself up but the indexer will see a spurious delete.
            LogTombstoneWithoutDelete(ex, id);
            throw new RegistryConcurrencyConflictException(
                id, ifMatchEtag, currentEtag: null, innerException: ex);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Original already gone — idempotent delete is fine.
            LogTombstoneWithoutDelete(ex, id);
        }
    }

    public async Task<ChildCount> CountChildrenAsync(
        Guid parentId,
        string environment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);

        // Partition-scoped GROUP BY entityType produces both the total and
        // the per-type breakdown in a single query (research §11).
        var definition = new QueryDefinition(
            "SELECT VALUE { entityType: c.entityType, count: COUNT(1) } " +
            "FROM c " +
            "WHERE c.parentId = @parentId AND (NOT IS_DEFINED(c._isTombstone) OR c._isTombstone = false) " +
            "GROUP BY c.entityType")
            .WithParameter("@parentId", parentId.ToString("D"));

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(environment),
        };

        var breakdown = new Dictionary<RegistryEntityType, int>();
        var total = 0;

        using var iterator = _container.GetItemQueryIterator<ChildCountBucket>(definition, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var bucket in page)
            {
                if (Enum.TryParse<RegistryEntityType>(bucket.EntityType, out var et))
                {
                    breakdown[et] = bucket.Count;
                    total += bucket.Count;
                }
            }
        }

        return new ChildCount(total, breakdown);
    }

    public async Task<RegistryEntity?> FindByParentAndNameAsync(
        Guid? parentId,
        RegistryEntityType entityType,
        string name,
        string environment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(environment);

        var sql = parentId.HasValue
            ? "SELECT TOP 1 * FROM c WHERE c.parentId = @parentId AND c.entityType = @entityType AND c.name = @name AND (NOT IS_DEFINED(c._isTombstone) OR c._isTombstone = false)"
            : "SELECT TOP 1 * FROM c WHERE NOT IS_DEFINED(c.parentId) AND c.entityType = @entityType AND c.name = @name AND (NOT IS_DEFINED(c._isTombstone) OR c._isTombstone = false)";

        var definition = new QueryDefinition(sql)
            .WithParameter("@entityType", entityType.ToString())
            .WithParameter("@name", name);

        if (parentId.HasValue)
        {
            definition = definition.WithParameter("@parentId", parentId.Value.ToString("D"));
        }

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(environment),
            MaxItemCount = 1,
        };

        using var iterator = _container.GetItemQueryIterator<RegistryEntityDocument>(definition, requestOptions: requestOptions);
        if (!iterator.HasMoreResults) return null;
        var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
        var doc = page.FirstOrDefault();
        return doc?.ToEntity();
    }

    public async Task<RegistryEntity?> FindParentAsync(
        Guid parentId,
        RegistryEntityType expectedParentType,
        string environment,
        CancellationToken cancellationToken)
    {
        var entity = await GetAsync(parentId, environment, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;
        return entity.EntityType == expectedParentType ? entity : null;
    }

    public async Task<RegistryEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        // Cross-partition single-document lookup. Bounded: each id is globally
        // unique so the result is at most one row. Used by GET/PUT/DELETE
        // endpoints when the caller omits `environment` from the query string.
        var definition = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id AND (NOT IS_DEFINED(c._isTombstone) OR c._isTombstone = false)")
            .WithParameter("@id", id.ToString("D"));

        using var iterator = _container.GetItemQueryIterator<RegistryEntityDocument>(definition);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            var doc = page.FirstOrDefault();
            if (doc is not null) return doc.ToEntity();
        }
        return null;
    }

    public async Task<RegistryEntity?> FindByAzureResourceIdAsync(
        string azureResourceId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(azureResourceId);

        // Spec 008 / FR-007. Case-insensitive cross-partition lookup. Bounded
        // by FR-007 ("an Azure namespace may only be onboarded once across
        // all environments") — result is at most one row.
        var definition = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE LOWER(c.azureResourceId) = LOWER(@armId) AND (NOT IS_DEFINED(c._isTombstone) OR c._isTombstone = false)")
            .WithParameter("@armId", azureResourceId);

        using var iterator = _container.GetItemQueryIterator<RegistryEntityDocument>(definition);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            var doc = page.FirstOrDefault();
            if (doc is not null) return doc.ToEntity();
        }
        return null;
    }

    public async Task<NamespaceInventoryPage> ListOnboardedAsync(
        NamespaceInventoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Cross-partition query against `source = Onboarded`. We accept the
        // RU cost here because (a) the inventory result set is bounded by
        // operator count (Constitution Decision Priorities: operational
        // simplicity > RU efficiency at v1), and (b) the wizard discipline
        // ensures Onboarded docs grow slowly. Composite indexes on
        // (source, lastValidatedAtUtc DESC) and (source, displayName) are a
        // v1.x optimization left to perf-baseline.md.
        var sql = new System.Text.StringBuilder(
            "SELECT * FROM c WHERE c.source = @source AND c.entityType = @entityType " +
            "AND (NOT IS_DEFINED(c._isTombstone) OR c._isTombstone = false)");
        var parameters = new List<(string Name, object Value)>
        {
            ("@source", nameof(RegistrySource.Onboarded)),
            ("@entityType", nameof(RegistryEntityType.Namespace)),
        };

        if (!string.IsNullOrWhiteSpace(query.Environment))
        {
            sql.Append(" AND c.environment = @environment");
            parameters.Add(("@environment", query.Environment));
        }

        if (query.LifecycleStatuses is { Count: > 0 })
        {
            var names = query.LifecycleStatuses.Select(s => s.ToString()).ToArray();
            sql.Append(" AND ARRAY_CONTAINS(@lifecycleStatuses, c.lifecycleStatus)");
            parameters.Add(("@lifecycleStatuses", names));
        }

        if (query.ValidationStatuses is { Count: > 0 })
        {
            var names = query.ValidationStatuses.Select(s => s.ToString()).ToArray();
            sql.Append(" AND ARRAY_CONTAINS(@validationStatuses, c.validationStatus)");
            parameters.Add(("@validationStatuses", names));
        }

        if (!query.IncludeArchived)
        {
            sql.Append(" AND (NOT IS_DEFINED(c.lifecycleStatus) OR c.lifecycleStatus != @archived)");
            parameters.Add(("@archived", nameof(Features.Namespaces.Shared.LifecycleStatus.Archived)));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Partial-name search across displayName + businessUnit (case-insensitive).
            sql.Append(" AND (CONTAINS(LOWER(c.displayName ?? ''), LOWER(@search)) " +
                       "OR CONTAINS(LOWER(c.businessUnit ?? ''), LOWER(@search)) " +
                       "OR CONTAINS(LOWER(c.name ?? ''), LOWER(@search)))");
            parameters.Add(("@search", query.Search));
        }

        if (!string.IsNullOrWhiteSpace(query.TagKey) && !string.IsNullOrWhiteSpace(query.TagValue))
        {
            sql.Append(" AND EXISTS (SELECT VALUE t FROM t IN c.tags " +
                       "WHERE LOWER(t.key) = LOWER(@tagKey) AND t.value = @tagValue)");
            parameters.Add(("@tagKey", query.TagKey));
            parameters.Add(("@tagValue", query.TagValue));
        }
        else if (!string.IsNullOrWhiteSpace(query.TagKey))
        {
            sql.Append(" AND EXISTS (SELECT VALUE t FROM t IN c.tags WHERE LOWER(t.key) = LOWER(@tagKey))");
            parameters.Add(("@tagKey", query.TagKey));
        }
        else if (!string.IsNullOrWhiteSpace(query.TagValue))
        {
            sql.Append(" AND EXISTS (SELECT VALUE t FROM t IN c.tags WHERE t.value = @tagValue)");
            parameters.Add(("@tagValue", query.TagValue));
        }

        sql.Append(query.Sort switch
        {
            NamespaceInventorySort.DisplayNameAsc => " ORDER BY c.displayName ASC",
            NamespaceInventorySort.DisplayNameDesc => " ORDER BY c.displayName DESC",
            NamespaceInventorySort.EnvironmentAsc => " ORDER BY c.environment ASC",
            NamespaceInventorySort.EnvironmentDesc => " ORDER BY c.environment DESC",
            NamespaceInventorySort.LastValidatedAtAsc => " ORDER BY c.lastValidatedAtUtc ASC",
            _ => " ORDER BY c.lastValidatedAtUtc DESC",
        });

        var definition = new QueryDefinition(sql.ToString());
        foreach (var (name, value) in parameters)
        {
            definition = definition.WithParameter(name, value);
        }

        var requestOptions = new QueryRequestOptions
        {
            MaxItemCount = query.PageSize,
        };

        using var iterator = _container.GetItemQueryIterator<RegistryEntityDocument>(
            definition,
            continuationToken: query.ContinuationToken,
            requestOptions: requestOptions);

        if (!iterator.HasMoreResults)
        {
            return new NamespaceInventoryPage(Array.Empty<RegistryNamespace>(), ContinuationToken: null);
        }

        var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
        var items = page
            .Select(d => d.ToEntity())
            .OfType<RegistryNamespace>()
            .ToArray();
        return new NamespaceInventoryPage(items, page.ContinuationToken);
    }

    public async Task<IReadOnlyList<string>> ListDistinctEnvironmentsAsync(CancellationToken cancellationToken)
    {
        // Cross-partition DISTINCT. Bounded result size — tenants configure a
        // small env set per FR-035 Assumptions. Used by GET /api/registry/environments
        // (T103c) which caches the result for 60s in IMemoryCache.
        var definition = new QueryDefinition("SELECT DISTINCT VALUE c.environment FROM c");
        var environments = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        using var iterator = _container.GetItemQueryIterator<string>(definition);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var env in page)
            {
                if (!string.IsNullOrEmpty(env))
                {
                    environments.Add(env);
                }
            }
        }
        return environments.ToArray();
    }

    private async Task<RegistryEntityType> ReadEntityTypeForTombstoneAsync(
        Guid id,
        string environment,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<RegistryEntityDocument>(
                id.ToString("D"),
                new PartitionKey(environment),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Resource?.EntityType ?? RegistryEntityType.Namespace;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // The original is already gone; the tombstone is informational.
            return RegistryEntityType.Namespace;
        }
    }

    // Internal-use shape for the GROUP BY result projection in CountChildrenAsync.
    private sealed record ChildCountBucket
    {
        [System.Text.Json.Serialization.JsonPropertyName("entityType")] public string EntityType { get; init; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("count")] public int Count { get; init; }
    }
}
