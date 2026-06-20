using System.Net;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Features.Discovery.Shared.Persistence;

// Spec 009 / T016 + data-model.md §1.2. Cosmos-backed IDiscoveryRunStore on
// the `discovery-runs` container (PK /namespaceId). Composite index
// (/namespaceId, /startedUtc DESC) powers the history-list view.
public sealed partial class CosmosDiscoveryRunStore : IDiscoveryRunStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosDiscoveryRunStore> _logger;

    public CosmosDiscoveryRunStore(
        CosmosClient client,
        IOptions<CosmosRegistryOptions> options,
        ILogger<CosmosDiscoveryRunStore> logger)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.Database, opts.DiscoveryRunsContainer);
        _logger = logger;
    }

    [LoggerMessage(EventId = 9201, Level = LogLevel.Information, Message = "DiscoveryRun {RunId} created for namespace {NamespaceId}.")]
    private partial void LogCreated(string runId, string namespaceId);

    [LoggerMessage(EventId = 9202, Level = LogLevel.Information, Message = "DiscoveryRun {RunId} transitioned to {Status} (namespace {NamespaceId}).")]
    private partial void LogStatusUpdated(string runId, string namespaceId, DiscoveryRunStatus status);

    public async Task<DiscoveryRun> CreateAsync(DiscoveryRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var doc = DiscoveryRunDocument.FromDomain(run);
        try
        {
            var response = await _container.CreateItemAsync(
                doc,
                new PartitionKey(run.NamespaceId),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            LogCreated(run.Id, run.NamespaceId);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException(
                $"A DiscoveryRun with id {run.Id} already exists for namespace {run.NamespaceId}.",
                ex);
        }
    }

    public async Task<DiscoveryRun?> GetAsync(string runId, string namespaceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        try
        {
            var response = await _container.ReadItemAsync<DiscoveryRunDocument>(
                runId,
                new PartitionKey(namespaceId),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<DiscoveryRunPage> ListByNamespaceAsync(
        string namespaceId,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        var clamped = Math.Clamp(pageSize, 1, 100);

        var definition = new QueryDefinition(
                "SELECT * FROM c WHERE c.namespaceId = @ns ORDER BY c.startedUtc DESC")
            .WithParameter("@ns", namespaceId);

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(namespaceId),
            MaxItemCount = clamped,
        };

        using var iterator = _container.GetItemQueryIterator<DiscoveryRunDocument>(
            definition, continuationToken: continuationToken, requestOptions: requestOptions);

        if (!iterator.HasMoreResults)
        {
            return new DiscoveryRunPage(Array.Empty<DiscoveryRun>(), ContinuationToken: null);
        }

        var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
        var items = page.Select(d => d.ToDomain()).ToArray();
        return new DiscoveryRunPage(items, page.ContinuationToken);
    }

    public async Task<DiscoveryRun> UpdateStatusAsync(
        string runId,
        string namespaceId,
        DiscoveryRunStatusUpdate update,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        var operations = new List<PatchOperation>();
        if (update.Status.HasValue) operations.Add(PatchOperation.Set("/status", update.Status.Value.ToString()));
        if (update.CompletedUtc.HasValue) operations.Add(PatchOperation.Set("/completedUtc", update.CompletedUtc.Value));
        if (update.DurationMs.HasValue) operations.Add(PatchOperation.Set("/durationMs", update.DurationMs.Value));
        if (update.QueueCount.HasValue) operations.Add(PatchOperation.Set("/queueCount", update.QueueCount.Value));
        if (update.TopicCount.HasValue) operations.Add(PatchOperation.Set("/topicCount", update.TopicCount.Value));
        if (update.SubscriptionCount.HasValue) operations.Add(PatchOperation.Set("/subscriptionCount", update.SubscriptionCount.Value));
        if (update.RuleCount.HasValue) operations.Add(PatchOperation.Set("/ruleCount", update.RuleCount.Value));
        if (update.NewCount.HasValue) operations.Add(PatchOperation.Set("/newCount", update.NewCount.Value));
        if (update.UpdatedCount.HasValue) operations.Add(PatchOperation.Set("/updatedCount", update.UpdatedCount.Value));
        if (update.UnchangedCount.HasValue) operations.Add(PatchOperation.Set("/unchangedCount", update.UnchangedCount.Value));
        if (update.MissingCount.HasValue) operations.Add(PatchOperation.Set("/missingCount", update.MissingCount.Value));
        if (update.Failure is not null) operations.Add(PatchOperation.Set("/failure", update.Failure));

        if (operations.Count == 0)
        {
            // No-op update; just read back the current state.
            var current = await GetAsync(runId, namespaceId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"DiscoveryRun {runId} not found in namespace {namespaceId}.");
            return current;
        }

        var requestOptions = new PatchItemRequestOptions();
        if (!string.IsNullOrEmpty(ifMatch)) requestOptions.IfMatchEtag = ifMatch;

        var response = await _container.PatchItemAsync<DiscoveryRunDocument>(
            runId,
            new PartitionKey(namespaceId),
            operations,
            requestOptions,
            cancellationToken).ConfigureAwait(false);

        if (update.Status.HasValue)
        {
            LogStatusUpdated(runId, namespaceId, update.Status.Value);
        }

        return response.Resource.ToDomain();
    }

    public async Task AppendCoalescedRequestAsync(
        string runId,
        string namespaceId,
        CoalescedRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Cosmos PATCH supports array Add at index "-" to append.
        var operation = PatchOperation.Add("/coalescedRequests/-", request);
        await _container.PatchItemAsync<DiscoveryRunDocument>(
            runId,
            new PartitionKey(namespaceId),
            new[] { operation },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
