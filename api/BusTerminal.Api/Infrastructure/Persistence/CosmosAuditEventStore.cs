using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 006 / data-model.md §5 + research §15. Append-only writes to
// `registry-audit` (PK `/entityId`). Reads are entity-scoped (partition-bound,
// `SELECT TOP @limit ... ORDER BY timestamp DESC`).
public sealed partial class CosmosAuditEventStore : IAuditEventStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosAuditEventStore> _logger;

    public CosmosAuditEventStore(
        CosmosClient client,
        IOptions<CosmosRegistryOptions> options,
        ILogger<CosmosAuditEventStore> logger)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.Database, opts.AuditContainer);
        _logger = logger;
    }

    [LoggerMessage(EventId = 6101, Level = LogLevel.Error, Message = "Audit-event append failed for entity {EntityId} (eventType={EventType}); entity write succeeded but audit history is incomplete.")]
    private partial void LogAuditAppendFailed(Exception exception, Guid entityId, AuditEventType eventType);

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        try
        {
            await _container.CreateItemAsync(
                auditEvent,
                new PartitionKey(auditEvent.EntityId.ToString()),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CosmosException ex)
        {
            // FR-032 records the audit failure but does NOT throw — the entity
            // write already succeeded and the API write path is the user-facing
            // contract. Loud structured log keeps the gap visible in App Insights.
            LogAuditAppendFailed(ex, auditEvent.EntityId, auditEvent.EventType);
        }
    }

    public async Task<IReadOnlyList<AuditEvent>> ListForEntityAsync(
        Guid entityId,
        int limit,
        CancellationToken cancellationToken)
    {
        var clamped = Math.Clamp(limit, 1, 200);
        var definition = new QueryDefinition($"SELECT TOP {clamped} * FROM c WHERE c.entityId = @entityId ORDER BY c.timestamp DESC")
            .WithParameter("@entityId", entityId.ToString());

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(entityId.ToString()),
            MaxItemCount = clamped,
        };

        var results = new List<AuditEvent>(clamped);
        using var iterator = _container.GetItemQueryIterator<AuditEvent>(definition, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var item in page)
            {
                results.Add(item);
                if (results.Count >= clamped) break;
            }
            if (results.Count >= clamped) break;
        }

        return results;
    }
}
