using System.Net;
using BusTerminal.Api.Features.Namespaces.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 008 / data-model.md §3 + research §6 + contracts/validation-run.schema.json.
// Append-only writes to the `namespace-validation-runs` container (PK
// `/namespaceId`). Reads are partition-scoped time-descending lookups; no
// update / delete surface — runs are immutable per FR-016.
//
// The container is provisioned by extending the existing
// iac/modules/cosmos-registry-store/ module; runtime resolution goes via
// CosmosRegistryOptions.ValidationRunsContainer.
public sealed partial class CosmosNamespaceValidationRunStore : INamespaceValidationRunStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosNamespaceValidationRunStore> _logger;

    public CosmosNamespaceValidationRunStore(
        CosmosClient client,
        IOptions<CosmosRegistryOptions> options,
        ILogger<CosmosNamespaceValidationRunStore> logger)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.Database, opts.ValidationRunsContainer);
        _logger = logger;
    }

    [LoggerMessage(EventId = 8001, Level = LogLevel.Error, Message = "ValidationRun append failed for namespace {NamespaceId} run {RunId}.")]
    private partial void LogAppendFailed(Exception exception, Guid namespaceId, Guid runId);

    public async Task AppendAsync(ValidationRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        var document = ValidationRunDocument.FromDomain(run);
        try
        {
            await _container.CreateItemAsync(
                document,
                new PartitionKey(run.NamespaceId.ToString("D")),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            LogAppendFailed(ex, run.NamespaceId, run.Id);
            throw new InvalidOperationException(
                $"A ValidationRun with id {run.Id:D} already exists for namespace {run.NamespaceId:D}.",
                ex);
        }
    }

    public async Task<ValidationRunPage> ListForNamespaceAsync(
        Guid namespaceId,
        int limit,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var clamped = Math.Clamp(limit, 1, 200);
        var definition = new QueryDefinition(
                "SELECT * FROM c WHERE c.namespaceId = @ns ORDER BY c.executedAtUtc DESC")
            .WithParameter("@ns", namespaceId.ToString("D"));

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(namespaceId.ToString("D")),
            MaxItemCount = clamped,
        };

        var items = new List<ValidationRun>(clamped);
        string? nextToken = null;
        using var iterator = _container.GetItemQueryIterator<ValidationRunDocument>(
            definition, continuationToken: continuationToken, requestOptions: requestOptions);

        while (iterator.HasMoreResults && items.Count < clamped)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var doc in page)
            {
                items.Add(doc.ToDomain());
                if (items.Count >= clamped) break;
            }
            nextToken = page.ContinuationToken;
        }

        return new ValidationRunPage(items, nextToken);
    }

    public async Task<ValidationRun?> GetAsync(
        Guid namespaceId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<ValidationRunDocument>(
                runId.ToString("D"),
                new PartitionKey(namespaceId.ToString("D")),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Resource.ToDomain(response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
