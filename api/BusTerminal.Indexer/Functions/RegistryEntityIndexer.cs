using System.Diagnostics;
using Azure.Search.Documents;
using BusTerminal.Indexer.Indexing;
using BusTerminal.Indexer.Indexing.Telemetry;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Indexer.Functions;

// Spec 006 / T048 / contracts/indexer-events.md §1, §3, §4, §5. Cosmos
// change-feed trigger on the `registry-entities` container. Tombstones
// (`_isTombstone = true`) translate to DeleteDocumentsAsync; everything else
// is a MergeOrUpload. Idempotent by construction (research §3).
public sealed partial class RegistryEntityIndexer
{
    private readonly SearchClient _searchClient;
    private readonly ISearchDocumentMapper _mapper;
    private readonly IPoisonHandler _poison;
    private readonly IndexerMeter _meter;
    private readonly ILogger<RegistryEntityIndexer> _logger;

    public RegistryEntityIndexer(
        SearchClient searchClient,
        ISearchDocumentMapper mapper,
        IPoisonHandler poison,
        IndexerMeter meter,
        ILogger<RegistryEntityIndexer> logger)
    {
        _searchClient = searchClient;
        _mapper = mapper;
        _poison = poison;
        _meter = meter;
        _logger = logger;
    }

    [LoggerMessage(EventId = 6401, Level = LogLevel.Information,
        Message = "Indexed {UpsertCount} upserts and {DeleteCount} deletes from {TotalCount} change-feed items.")]
    private partial void LogBatchProcessed(int upsertCount, int deleteCount, int totalCount);

    [Function("RegistryEntityIndexer")]
    public Task RunAsync(
        [CosmosDBTrigger(
            databaseName: "%COSMOS_DATABASE_NAME%",
            containerName: "%COSMOS_REGISTRY_ENTITIES_CONTAINER%",
            Connection = "Cosmos",
            LeaseContainerName = "%COSMOS_REGISTRY_LEASES_CONTAINER%",
            CreateLeaseContainerIfNotExists = false,
            MaxItemsPerInvocation = 100,
            StartFromBeginning = true)]
        IReadOnlyList<RegistryEntityChangeFeedItem> changes,
        FunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(context);
        // The Functions host owns the FunctionContext (retry attempt +
        // cancellation); unpack it here so the metric-bearing core is
        // independently testable without a fake FunctionContext.
        return ProcessBatchAsync(changes, GetRetryCount(context), context.CancellationToken);
    }

    internal async Task ProcessBatchAsync(
        IReadOnlyList<RegistryEntityChangeFeedItem> changes,
        int retryCount,
        CancellationToken cancellationToken)
    {
        if (changes.Count == 0) return;

        _meter.BatchSize.Record(changes.Count);

        // Split tombstones from upserts so the SDK calls go in two batches —
        // the SDK supports mixed-action batches but keeping them split makes
        // the OTel spans cleaner and the retry semantics narrower.
        var upserts = new List<IReadOnlyDictionary<string, object?>>(changes.Count);
        var deletes = new List<string>();

        foreach (var change in changes)
        {
            if (change.IsTombstone)
            {
                if (!string.IsNullOrEmpty(change.TombstoneFor))
                {
                    deletes.Add(change.TombstoneFor);
                }
                continue;
            }
            upserts.Add(_mapper.ToSearchDocument(change));
        }

        try
        {
            if (upserts.Count > 0)
            {
                var sw = Stopwatch.StartNew();
                await _searchClient.MergeOrUploadDocumentsAsync(upserts, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                _meter.AiSearchDuration.Record(sw.Elapsed.TotalMilliseconds, UpsertTag);
                _meter.DocumentsIndexed.Add(upserts.Count, UpsertTag);
            }

            if (deletes.Count > 0)
            {
                var sw = Stopwatch.StartNew();
                await _searchClient.DeleteDocumentsAsync("id", deletes, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                _meter.AiSearchDuration.Record(sw.Elapsed.TotalMilliseconds, DeleteTag);
                _meter.DocumentsIndexed.Add(deletes.Count, DeleteTag);
            }

            LogBatchProcessed(upserts.Count, deletes.Count, changes.Count);
        }
        catch (Exception ex)
        {
            // research §3 — Cosmos change-feed checkpoints only after a
            // successful return. Rethrow after surfacing the first failed
            // item so the indexer keeps processing on a retry; the host's
            // MaxRetryCount governs the bounded-retry behavior.
            var firstUpsert = changes.FirstOrDefault(c => !c.IsTombstone);
            var firstDelete = changes.FirstOrDefault(c => c.IsTombstone);
            var subject = firstUpsert ?? firstDelete ?? changes[0];
            var category = ClassifyError(ex);
            _meter.Failures.Add(1, new KeyValuePair<string, object?>(IndexerMeter.TagCategory, category));
            _poison.HandlePermanentFailure(
                subject,
                firstUpsert is null ? "delete" : "upsert",
                category,
                retryCount: retryCount,
                cause: ex);
            throw;
        }
    }

    private static readonly KeyValuePair<string, object?> UpsertTag =
        new(IndexerMeter.TagOperation, IndexerMeter.OperationUpsert);

    private static readonly KeyValuePair<string, object?> DeleteTag =
        new(IndexerMeter.TagOperation, IndexerMeter.OperationDelete);

    private static string ClassifyError(Exception ex) => ex switch
    {
        Azure.RequestFailedException rfe when rfe.Status is 401 or 403 => "unauthorized",
        Azure.RequestFailedException rfe when rfe.Status is 400 or 422 => "aiSearchSchema",
        Azure.RequestFailedException rfe when rfe.Status >= 500 => "transient",
        InvalidOperationException => "mapping",
        _ => "transient",
    };

    private static int GetRetryCount(FunctionContext context)
    {
        // The Functions worker host exposes the current retry attempt via
        // FunctionContext.Items when the binding supports it. We don't depend
        // on it being present — when absent we report -1 so the log filter is
        // unambiguous about the gap.
        if (context.Items.TryGetValue("FunctionRetryAttempt", out var value) && value is int attempt)
        {
            return attempt;
        }
        return -1;
    }
}
