using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Lifecycle;
using BusTerminal.Api.Domain.Relationships;
using BusTerminal.Api.Domain.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 004 / FR-020 + FR-025 / Q2 + Q5. Reads filter `isDeleted = false` unless
// includeDeleted is passed. Writes use IfMatch with the resource's ConcurrencyToken
// — Cosmos 412 -> ConcurrencyConflictException. Every successful write triggers an
// IChangeEventLog.AppendAsync (failure logged but the resource write is NOT rolled
// back, per data-model.md §Change-event log emission).
public sealed partial class CosmosCanonicalResourceStore : ICanonicalResourceStore
{
    private readonly Container _container;
    private readonly IChangeEventLog _changeEventLog;
    private readonly JsonResourceSerializer _serializer;
    private readonly TimeProvider _time;
    private readonly ILogger<CosmosCanonicalResourceStore> _logger;

    public CosmosCanonicalResourceStore(
        CosmosClient client,
        IOptions<CosmosOptions> options,
        IChangeEventLog changeEventLog,
        JsonResourceSerializer serializer,
        TimeProvider time,
        ILogger<CosmosCanonicalResourceStore> logger)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.Database, opts.Containers.Resources);
        _changeEventLog = changeEventLog;
        _serializer = serializer;
        _time = time;
        _logger = logger;
    }

    [LoggerMessage(EventId = 4101, Level = LogLevel.Error, Message = "ChangeEvent append failed for resource {ResourceId} ({EventType}); canonical write succeeded but history is incomplete.")]
    private partial void LogChangeEventAppendFailure(Exception exception, ResourceId resourceId, ChangeEventType eventType);

    public async Task<Resource?> GetAsync(
        ResourceId id,
        string resourceTypeDiscriminator,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<Resource>(
                id.ToString(),
                new PartitionKey(resourceTypeDiscriminator),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Resource is null)
            {
                return null;
            }

            return (!includeDeleted && response.Resource.IsDeleted) ? null : WithEtag(response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<Resource> QueryAsync(
        ResourceQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (sql, parameters, partitionKey) = BuildQuery(query);

        var requestOptions = partitionKey.HasValue
            ? new QueryRequestOptions { PartitionKey = partitionKey.Value }
            : new QueryRequestOptions();

        var definition = new QueryDefinition(sql);
        foreach (var (name, value) in parameters)
        {
            definition = definition.WithParameter(name, value);
        }

        using var iterator = _container.GetItemQueryIterator<Resource>(definition, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var item in page)
            {
                yield return item;
            }
        }
    }

    public async Task<Resource> CreateAsync(
        Resource resource,
        PrincipalReference actor,
        string? sourceSystem,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(actor);

        var stamped = StampForCreate(resource, actor, sourceSystem);
        var response = await _container.CreateItemAsync(
            stamped,
            new PartitionKey(stamped.ResourceType),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var withToken = WithEtag(response.Resource, response.ETag);
        await AppendChangeEventAsync(
            withToken,
            ChangeEventType.Created,
            actor,
            sourceSystem,
            previousToken: null,
            lifecycleBefore: null,
            lifecycleAfter: withToken.Lifecycle,
            cancellationToken).ConfigureAwait(false);

        return withToken;
    }

    public async Task<Resource> UpdateAsync(
        Resource resource,
        PrincipalReference actor,
        string? sourceSystem,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(actor);

        var presentedToken = resource.ConcurrencyToken;
        var existing = await GetAsync(resource.Id, resource.ResourceType, includeDeleted: true, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Cannot update non-existent resource {resource.Id}.");

        var stamped = StampForUpdate(resource, existing, actor, sourceSystem);
        try
        {
            var response = await _container.ReplaceItemAsync(
                stamped,
                stamped.Id.ToString(),
                new PartitionKey(stamped.ResourceType),
                requestOptions: new ItemRequestOptions { IfMatchEtag = presentedToken.Value },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var withToken = WithEtag(response.Resource, response.ETag);
            var eventType = existing.Lifecycle != withToken.Lifecycle
                ? ChangeEventType.LifecycleTransitioned
                : ChangeEventType.Updated;

            await AppendChangeEventAsync(
                withToken,
                eventType,
                actor,
                sourceSystem,
                previousToken: presentedToken,
                lifecycleBefore: existing.Lifecycle,
                lifecycleAfter: withToken.Lifecycle,
                cancellationToken).ConfigureAwait(false);

            return withToken;
        }
        catch (CosmosException ex) when (ConcurrencyExceptionMapper.TryMap(ex, resource.Id, presentedToken, out var mapped))
        {
            throw mapped!;
        }
    }

    public async Task<Resource> SoftDeleteAsync(
        ResourceId id,
        string resourceTypeDiscriminator,
        ConcurrencyToken token,
        PrincipalReference actor,
        string? sourceSystem,
        CancellationToken cancellationToken)
    {
        var existing = await GetAsync(id, resourceTypeDiscriminator, includeDeleted: true, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Cannot soft-delete non-existent resource {id}.");

        if (existing.IsDeleted)
        {
            return existing;
        }

        var marked = existing with
        {
            IsDeleted = true,
            Audit = existing.Audit with { ModifiedBy = actor, ModifiedAt = _time.GetUtcNow(), SourceSystem = sourceSystem },
        };

        try
        {
            var response = await _container.ReplaceItemAsync(
                marked,
                id.ToString(),
                new PartitionKey(resourceTypeDiscriminator),
                requestOptions: new ItemRequestOptions { IfMatchEtag = token.Value },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var withToken = WithEtag(response.Resource, response.ETag);

            await AppendChangeEventAsync(
                withToken,
                ChangeEventType.SoftDeleted,
                actor,
                sourceSystem,
                previousToken: token,
                lifecycleBefore: existing.Lifecycle,
                lifecycleAfter: existing.Lifecycle,
                cancellationToken).ConfigureAwait(false);

            return withToken;
        }
        catch (CosmosException ex) when (ConcurrencyExceptionMapper.TryMap(ex, id, token, out var mapped))
        {
            throw mapped!;
        }
    }

    public async Task<Resource> RestoreAsync(
        ResourceId id,
        string resourceTypeDiscriminator,
        ConcurrencyToken token,
        PrincipalReference actor,
        string? sourceSystem,
        CancellationToken cancellationToken)
    {
        var existing = await GetAsync(id, resourceTypeDiscriminator, includeDeleted: true, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Cannot restore non-existent resource {id}.");

        if (!existing.IsDeleted)
        {
            return existing;
        }

        var restored = existing with
        {
            IsDeleted = false,
            Audit = existing.Audit with { ModifiedBy = actor, ModifiedAt = _time.GetUtcNow(), SourceSystem = sourceSystem },
        };

        try
        {
            var response = await _container.ReplaceItemAsync(
                restored,
                id.ToString(),
                new PartitionKey(resourceTypeDiscriminator),
                requestOptions: new ItemRequestOptions { IfMatchEtag = token.Value },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var withToken = WithEtag(response.Resource, response.ETag);

            await AppendChangeEventAsync(
                withToken,
                ChangeEventType.Restored,
                actor,
                sourceSystem,
                previousToken: token,
                lifecycleBefore: existing.Lifecycle,
                lifecycleAfter: existing.Lifecycle,
                cancellationToken).ConfigureAwait(false);

            return withToken;
        }
        catch (CosmosException ex) when (ConcurrencyExceptionMapper.TryMap(ex, id, token, out var mapped))
        {
            throw mapped!;
        }
    }

    private static Resource WithEtag(Resource resource, string etag) =>
        resource with { ConcurrencyToken = new ConcurrencyToken(etag) };

    private Resource StampForCreate(Resource resource, PrincipalReference actor, string? sourceSystem)
    {
        var now = _time.GetUtcNow();
        var existingAudit = resource.Audit;
        var audit = existingAudit with
        {
            CreatedBy = actor,
            CreatedAt = now,
            ModifiedBy = actor,
            ModifiedAt = now,
            SourceSystem = sourceSystem,
        };

        return resource with { Audit = audit };
    }

    private Resource StampForUpdate(
        Resource incoming,
        Resource existing,
        PrincipalReference actor,
        string? sourceSystem)
    {
        var audit = existing.Audit with
        {
            ModifiedBy = actor,
            ModifiedAt = _time.GetUtcNow(),
            SourceSystem = sourceSystem,
        };

        return incoming with { Audit = audit };
    }

    private async Task AppendChangeEventAsync(
        Resource resource,
        ChangeEventType eventType,
        PrincipalReference actor,
        string? sourceSystem,
        ConcurrencyToken? previousToken,
        LifecycleState? lifecycleBefore,
        LifecycleState lifecycleAfter,
        CancellationToken cancellationToken)
    {
        try
        {
            using var snapshot = JsonDocument.Parse(_serializer.SerializeToJson(resource));
            var evt = new ChangeEvent(
                Id: Guid.NewGuid(),
                ResourceId: resource.Id,
                ResourceType: resource.ResourceType,
                EventType: eventType,
                Actor: actor,
                Timestamp: _time.GetUtcNow(),
                ConcurrencyTokenAfter: resource.ConcurrencyToken,
                ConcurrencyTokenBefore: previousToken,
                LifecycleBefore: lifecycleBefore,
                LifecycleAfter: lifecycleAfter,
                SourceSystem: sourceSystem,
                Diff: null,
                Snapshot: snapshot.RootElement.Clone());

            await _changeEventLog.AppendAsync(evt, cancellationToken).ConfigureAwait(false);
        }
        catch (CosmosException ex)
        {
            LogChangeEventAppendFailure(ex, resource.Id, eventType);
            throw;
        }
    }

    private static (string Sql, IReadOnlyList<(string Name, object Value)> Parameters, PartitionKey? Pk) BuildQuery(ResourceQuery query)
    {
        return query switch
        {
            ResourceQuery.All all => BuildAllQuery(all),
            ResourceQuery.OwnedByTeam owned => (
                BaseSelect("c.ownership.owningTeamId = @teamId", owned.IncludeDeleted),
                [("@teamId", owned.TeamId.ToString())],
                Pk: null),
            ResourceQuery.InEnvironment env => (
                BaseSelect("ARRAY_CONTAINS(c.environments, @env)", env.IncludeDeleted),
                [("@env", env.Environment.Value)],
                Pk: null),
            ResourceQuery.ByNamespacePath ns => (
                BaseSelect("c.namespacePath = @ns", ns.IncludeDeleted),
                [("@ns", ns.Path.Value)],
                Pk: null),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query, "Unhandled ResourceQuery variant."),
        };
    }

    private static (string Sql, IReadOnlyList<(string Name, object Value)> Parameters, PartitionKey? Pk) BuildAllQuery(ResourceQuery.All all)
    {
        if (all.ResourceTypeDiscriminator is null)
        {
            return (BaseSelect(predicate: null, all.IncludeDeleted), Array.Empty<(string, object)>(), Pk: null);
        }

        return (
            BaseSelect(predicate: null, all.IncludeDeleted),
            Array.Empty<(string, object)>(),
            Pk: new PartitionKey(all.ResourceTypeDiscriminator));
    }

    private static string BaseSelect(string? predicate, bool includeDeleted)
    {
        // Resource queries always exclude relationship peer documents — the
        // Resource converter cannot deserialize them and they have their own
        // query surface (QueryRelationshipsAsync).
        const string ExcludeRelationships = "c.resourceType != 'relationship'";

        var fullPredicate = predicate is null
            ? ExcludeRelationships
            : $"{ExcludeRelationships} AND {predicate}";

        var where = includeDeleted
            ? $" WHERE {fullPredicate}"
            : $" WHERE c.isDeleted = false AND {fullPredicate}";

        return $"SELECT * FROM c{where}";
    }

    // -----------------------------------------------------------------------
    // Spec 004 / T104 — Relationship CRUD + query surface.
    // Relationships are peer documents in the same `resources` container under
    // partition key /resourceType = "relationship". They do not emit change
    // events in v1 (the change-event log is keyed by ResourceId of a Resource);
    // a future slice can extend if relationship history becomes important.
    // -----------------------------------------------------------------------

    public async Task<Relationship> CreateRelationshipAsync(
        Relationship relationship,
        PrincipalReference actor,
        string? sourceSystem,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(actor);

        var now = _time.GetUtcNow();
        var stamped = relationship with
        {
            ResourceType = ResourceTypeDiscriminators.Relationship,
            Audit = relationship.Audit with
            {
                CreatedBy = actor,
                CreatedAt = now,
                ModifiedBy = actor,
                ModifiedAt = now,
                SourceSystem = sourceSystem,
            },
        };

        var response = await _container.CreateItemAsync(
            stamped,
            new PartitionKey(ResourceTypeDiscriminators.Relationship),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response.Resource with { ConcurrencyToken = new ConcurrencyToken(response.ETag) };
    }

    public async Task<Relationship?> GetRelationshipAsync(
        ResourceId id,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<Relationship>(
                id.ToString(),
                new PartitionKey(ResourceTypeDiscriminators.Relationship),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Resource is null)
            {
                return null;
            }

            return (!includeDeleted && response.Resource.IsDeleted)
                ? null
                : response.Resource with { ConcurrencyToken = new ConcurrencyToken(response.ETag) };
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<Relationship> QueryRelationshipsAsync(
        RelationshipQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (sql, parameters) = BuildRelationshipQuery(query);

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(ResourceTypeDiscriminators.Relationship),
        };

        var definition = new QueryDefinition(sql);
        foreach (var (name, value) in parameters)
        {
            definition = definition.WithParameter(name, value);
        }

        using var iterator = _container.GetItemQueryIterator<Relationship>(definition, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var item in page)
            {
                yield return item;
            }
        }
    }

    private static (string Sql, IReadOnlyList<(string Name, object Value)> Parameters) BuildRelationshipQuery(RelationshipQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query switch
        {
            RelationshipQuery.All all => (
                BaseRelSelect(predicate: null, all.IncludeDeleted),
                Array.Empty<(string, object)>()),

            RelationshipQuery.ByType byType => (
                BaseRelSelect("c.type = @type", byType.IncludeDeleted),
                [("@type", RelationshipTypeWire.Of(byType.Type))]),

            RelationshipQuery.ByEndpoint byEndpoint => (
                BaseRelSelect(BuildEndpointPredicate(byEndpoint.Direction), byEndpoint.IncludeDeleted),
                [("@id", byEndpoint.EndpointId.ToString())]),

            _ => throw new ArgumentOutOfRangeException(nameof(query), query, "Unhandled RelationshipQuery variant."),
        };
    }

    private static string BuildEndpointPredicate(Direction direction) => direction switch
    {
        Direction.Outbound => "c.sourceId = @id",
        Direction.Inbound => "c.targetId = @id",
        Direction.Both => "(c.sourceId = @id OR c.targetId = @id)",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unhandled Direction."),
    };

    private static string BaseRelSelect(string? predicate, bool includeDeleted)
    {
        var typeFilter = "c.resourceType = 'relationship'";

        var fullPredicate = predicate is null
            ? typeFilter
            : $"{typeFilter} AND {predicate}";

        var where = includeDeleted
            ? $" WHERE {fullPredicate}"
            : $" WHERE c.isDeleted = false AND {fullPredicate}";

        return $"SELECT * FROM c{where}";
    }
}

// Wire-format helper for RelationshipType — the enum's JsonStringEnumConverter
// already emits camelCase strings in JSON, but ad-hoc SQL parameters bypass that
// converter so the WHERE clause needs an explicit camelCase mapping.
internal static class RelationshipTypeWire
{
    public static string Of(RelationshipType type) => type switch
    {
        RelationshipType.PublishesTo => "publishesTo",
        RelationshipType.ConsumedBy => "consumedBy",
        RelationshipType.SubscriptionOf => "subscriptionOf",
        RelationshipType.UsesContract => "usesContract",
        RelationshipType.Owns => "owns",
        RelationshipType.AttachedTo => "attachedTo",
        RelationshipType.Replaces => "replaces",
        RelationshipType.PartOfFlow => "partOfFlow",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown RelationshipType."),
    };
}
