using System.Collections.Concurrent;
using BusTerminal.Api.Features.Namespaces.Inventory;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;

namespace BusTerminal.Api.Tests.Features.Registry.Fakes;

// Spec 006 / Phase 3 US1. In-process fakes used by the contract tests so the
// REST surface can be exercised without a live Cosmos account. Behaviour is
// constrained to the bits the registry endpoints depend on: ETag-based
// optimistic concurrency, tombstone exclusion, paginated list, child-count.
public sealed class InMemoryRegistryEntityStore : IRegistryEntityStore
{
    private readonly ConcurrentDictionary<Guid, Stored> _items = new();

    public IReadOnlyDictionary<Guid, RegistryEntity> Snapshot()
        => _items.ToDictionary(kv => kv.Key, kv => kv.Value.Entity);

    public Task<RegistryEntity?> GetAsync(Guid id, string environment, CancellationToken cancellationToken)
    {
        if (_items.TryGetValue(id, out var existing)
            && existing.Entity.Environment == environment)
        {
            return Task.FromResult<RegistryEntity?>(existing.Entity);
        }
        return Task.FromResult<RegistryEntity?>(null);
    }

    public Task<RegistryEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_items.TryGetValue(id, out var existing))
        {
            return Task.FromResult<RegistryEntity?>(existing.Entity);
        }
        return Task.FromResult<RegistryEntity?>(null);
    }

    public Task<RegistryEntity?> FindByAzureResourceIdAsync(string azureResourceId, CancellationToken cancellationToken)
    {
        var match = _items.Values
            .Select(s => s.Entity)
            .FirstOrDefault(e => string.Equals(e.AzureResourceId, azureResourceId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task<RegistryEntityPage> ListAsync(RegistryEntityListQuery query, CancellationToken cancellationToken)
    {
        var items = _items.Values
            .Where(s => s.Entity.Environment == query.Environment)
            .Select(s => s.Entity)
            .Where(e => !query.EntityType.HasValue || e.EntityType == query.EntityType.Value)
            .Where(e => !query.ParentId.HasValue || e.ParentId == query.ParentId.Value)
            .Where(e => !query.Status.HasValue || e.Status == query.Status.Value)
            .OrderByDescending(e => e.UpdatedAtUtc)
            .Take(query.PageSize)
            .ToList();
        return Task.FromResult(new RegistryEntityPage(items, ContinuationToken: null));
    }

    public Task<RegistryEntity> CreateAsync(RegistryEntity entity, CancellationToken cancellationToken)
    {
        var etag = "\"" + Guid.NewGuid().ToString("N") + "\"";
        var stamped = entity with { Etag = etag };
        if (!_items.TryAdd(entity.Id, new Stored(stamped, etag)))
        {
            throw new InvalidOperationException($"Entity {entity.Id} already exists.");
        }
        return Task.FromResult(stamped);
    }

    public Task<RegistryEntity> UpdateAsync(RegistryEntity entity, string ifMatchEtag, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(entity.Id, out var existing))
        {
            throw new InvalidOperationException("Entity not found.");
        }
        if (!string.Equals(existing.Etag, ifMatchEtag, StringComparison.Ordinal))
        {
            throw new RegistryConcurrencyConflictException(entity.Id, ifMatchEtag, existing.Etag);
        }
        var etag = "\"" + Guid.NewGuid().ToString("N") + "\"";
        var stamped = entity with { Etag = etag };
        _items[entity.Id] = new Stored(stamped, etag);
        return Task.FromResult(stamped);
    }

    public Task DeleteAsync(Guid id, string environment, string ifMatchEtag, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(id, out var existing))
        {
            return Task.CompletedTask;
        }
        if (!string.Equals(existing.Etag, ifMatchEtag, StringComparison.Ordinal))
        {
            throw new RegistryConcurrencyConflictException(id, ifMatchEtag, existing.Etag);
        }
        _items.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<ChildCount> CountChildrenAsync(Guid parentId, string environment, CancellationToken cancellationToken)
    {
        var children = _items.Values
            .Where(s => s.Entity.Environment == environment && s.Entity.ParentId == parentId)
            .GroupBy(s => s.Entity.EntityType)
            .ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult(new ChildCount(
            children.Values.Sum(),
            (IReadOnlyDictionary<RegistryEntityType, int>)children));
    }

    public Task<RegistryEntity?> FindByParentAndNameAsync(
        Guid? parentId,
        RegistryEntityType entityType,
        string name,
        string environment,
        CancellationToken cancellationToken)
    {
        var match = _items.Values
            .Select(s => s.Entity)
            .FirstOrDefault(e =>
                e.Environment == environment
                && e.EntityType == entityType
                && string.Equals(e.Name, name, StringComparison.Ordinal)
                && e.ParentId == parentId);
        return Task.FromResult<RegistryEntity?>(match);
    }

    public Task<RegistryEntity?> FindParentAsync(
        Guid parentId,
        RegistryEntityType expectedParentType,
        string environment,
        CancellationToken cancellationToken)
    {
        if (_items.TryGetValue(parentId, out var existing)
            && existing.Entity.EntityType == expectedParentType
            && existing.Entity.Environment == environment)
        {
            return Task.FromResult<RegistryEntity?>(existing.Entity);
        }
        return Task.FromResult<RegistryEntity?>(null);
    }

    public Task<IReadOnlyList<string>> ListDistinctEnvironmentsAsync(CancellationToken cancellationToken)
    {
        var envs = _items.Values
            .Select(s => s.Entity.Environment)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyList<string>>(envs);
    }

    public Task<NamespaceInventoryPage> ListOnboardedAsync(
        NamespaceInventoryQuery query,
        CancellationToken cancellationToken)
    {
        var items = _items.Values
            .Select(s => s.Entity)
            .OfType<RegistryNamespace>()
            .Where(n => n.Source == RegistrySource.Onboarded);

        if (!string.IsNullOrWhiteSpace(query.Environment))
        {
            items = items.Where(n => string.Equals(n.Environment, query.Environment, StringComparison.Ordinal));
        }

        if (query.LifecycleStatuses is { Count: > 0 })
        {
            var set = query.LifecycleStatuses.ToHashSet();
            items = items.Where(n => n.LifecycleStatus.HasValue && set.Contains(n.LifecycleStatus.Value));
        }

        if (query.ValidationStatuses is { Count: > 0 })
        {
            var set = query.ValidationStatuses.ToHashSet();
            items = items.Where(n => n.ValidationStatus.HasValue && set.Contains(n.ValidationStatus.Value));
        }

        if (!query.IncludeArchived)
        {
            items = items.Where(n => n.LifecycleStatus != LifecycleStatus.Archived);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search;
            items = items.Where(n =>
                (n.DisplayName?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                || (n.BusinessUnit?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                || n.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.TagKey) && !string.IsNullOrWhiteSpace(query.TagValue))
        {
            items = items.Where(n => n.Tags.Any(t =>
                string.Equals(t.Key, query.TagKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Value, query.TagValue, StringComparison.Ordinal)));
        }
        else if (!string.IsNullOrWhiteSpace(query.TagKey))
        {
            items = items.Where(n => n.Tags.Any(t =>
                string.Equals(t.Key, query.TagKey, StringComparison.OrdinalIgnoreCase)));
        }
        else if (!string.IsNullOrWhiteSpace(query.TagValue))
        {
            items = items.Where(n => n.Tags.Any(t =>
                string.Equals(t.Value, query.TagValue, StringComparison.Ordinal)));
        }

        items = query.Sort switch
        {
            NamespaceInventorySort.DisplayNameAsc => items.OrderBy(n => n.DisplayName, StringComparer.OrdinalIgnoreCase),
            NamespaceInventorySort.DisplayNameDesc => items.OrderByDescending(n => n.DisplayName, StringComparer.OrdinalIgnoreCase),
            NamespaceInventorySort.EnvironmentAsc => items.OrderBy(n => n.Environment, StringComparer.OrdinalIgnoreCase),
            NamespaceInventorySort.EnvironmentDesc => items.OrderByDescending(n => n.Environment, StringComparer.OrdinalIgnoreCase),
            NamespaceInventorySort.LastValidatedAtAsc => items.OrderBy(n => n.LastValidatedAtUtc),
            _ => items.OrderByDescending(n => n.LastValidatedAtUtc),
        };

        var page = items.Take(query.PageSize).ToArray();
        return Task.FromResult(new NamespaceInventoryPage(page, ContinuationToken: null));
    }

    public void Clear() => _items.Clear();

    private sealed record Stored(RegistryEntity Entity, string Etag);
}

public sealed class InMemoryAuditEventStore : IAuditEventStore
{
    private readonly ConcurrentBag<AuditEvent> _events = new();

    public IReadOnlyList<AuditEvent> All() => _events.ToArray();

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        _events.Add(auditEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> ListForEntityAsync(Guid entityId, int limit, CancellationToken cancellationToken)
    {
        var items = _events
            .Where(e => e.EntityId == entityId)
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToArray();
        return Task.FromResult<IReadOnlyList<AuditEvent>>(items);
    }

    public void Clear()
    {
        while (_events.TryTake(out _)) { }
    }
}
