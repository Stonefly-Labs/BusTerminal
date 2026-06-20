using System.Net;
using System.Text.Json.Serialization;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Features.Discovery.Shared.Persistence;

// Spec 009 / T017 + R-03. Cosmos-backed IDiscoveryLockStore. The lock is a
// single document per namespace (id = "lock", PK = namespaceId). Acquisition
// is atomic via ETag-based optimistic concurrency.
//
// Algorithm (data-model.md §1.3):
//   1. Read the lock document.
//   2. Not found → CreateItemAsync. Conflict on Create → race lost; retry.
//   3. Found + currentRunId null → ReplaceItemAsync with IfMatch. 412 → race; retry.
//   4. Found + currentRunId set + expectedReleaseByUtc > now → COALESCE.
//   5. Found + currentRunId set + expectedReleaseByUtc <= now → STEAL via
//      ReplaceItemAsync with IfMatch. 412 → race; retry.
public sealed partial class CosmosDiscoveryLockStore : IDiscoveryLockStore
{
    private const string LockDocumentId = "lock";
    private const int MaxRetries = 5;

    private readonly Container _container;
    private readonly int _expirySeconds;
    private readonly ILogger<CosmosDiscoveryLockStore> _logger;
    private readonly TimeProvider _time;

    public CosmosDiscoveryLockStore(
        CosmosClient client,
        IOptions<CosmosRegistryOptions> options,
        TimeProvider time,
        ILogger<CosmosDiscoveryLockStore> logger)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.Database, opts.DiscoveryLocksContainer);
        _expirySeconds = opts.DiscoveryLockExpirySeconds;
        _time = time;
        _logger = logger;
    }

    [LoggerMessage(EventId = 9301, Level = LogLevel.Information, Message = "Discovery lock {Outcome} for namespace {NamespaceId} (active run {RunId}, stolen {StolenRunId}).")]
    private partial void LogAcquisition(string namespaceId, DiscoveryLockOutcome outcome, string runId, string? stolenRunId);

    public async Task<DiscoveryLockAcquisition> TryAcquireAsync(
        string namespaceId,
        string newRunId,
        string podId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(podId);

        var partitionKey = new PartitionKey(namespaceId);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            DiscoveryLockDocument? existing = null;
            string? existingEtag = null;

            try
            {
                var read = await _container.ReadItemAsync<DiscoveryLockDocument>(
                    LockDocumentId, partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
                existing = read.Resource;
                existingEtag = read.ETag;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Path 2 — create.
                var fresh = NewLockDocument(namespaceId, newRunId, podId);
                try
                {
                    await _container.CreateItemAsync(fresh, partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
                    LogAcquisition(namespaceId, DiscoveryLockOutcome.Acquired, newRunId, null);
                    return new DiscoveryLockAcquisition(DiscoveryLockOutcome.Acquired, newRunId, null);
                }
                catch (CosmosException createConflict) when (createConflict.StatusCode == HttpStatusCode.Conflict)
                {
                    // Race — another acquirer created concurrently. Retry.
                    continue;
                }
            }

            var now = _time.GetUtcNow();
            if (string.IsNullOrEmpty(existing!.CurrentRunId))
            {
                // Path 3 — open lock; take it.
                var replaced = existing with
                {
                    CurrentRunId = newRunId,
                    AcquiredUtc = now,
                    AcquiredByPodId = podId,
                    ExpectedReleaseByUtc = now.AddSeconds(_expirySeconds),
                };
                if (await TryReplaceWithIfMatchAsync(replaced, partitionKey, existingEtag!, cancellationToken).ConfigureAwait(false))
                {
                    LogAcquisition(namespaceId, DiscoveryLockOutcome.Acquired, newRunId, null);
                    return new DiscoveryLockAcquisition(DiscoveryLockOutcome.Acquired, newRunId, null);
                }
                continue;
            }

            if (existing.ExpectedReleaseByUtc is { } expectedRelease && expectedRelease > now)
            {
                // Path 4 — coalesce.
                LogAcquisition(namespaceId, DiscoveryLockOutcome.Coalesced, existing.CurrentRunId, null);
                return new DiscoveryLockAcquisition(DiscoveryLockOutcome.Coalesced, existing.CurrentRunId, null);
            }

            // Path 5 — steal.
            var stolenRunId = existing.CurrentRunId;
            var stealReplacement = existing with
            {
                CurrentRunId = newRunId,
                AcquiredUtc = now,
                AcquiredByPodId = podId,
                ExpectedReleaseByUtc = now.AddSeconds(_expirySeconds),
            };
            if (await TryReplaceWithIfMatchAsync(stealReplacement, partitionKey, existingEtag!, cancellationToken).ConfigureAwait(false))
            {
                LogAcquisition(namespaceId, DiscoveryLockOutcome.Stolen, newRunId, stolenRunId);
                return new DiscoveryLockAcquisition(DiscoveryLockOutcome.Stolen, newRunId, stolenRunId);
            }
            // Race on steal — another acquirer claimed it. Retry.
        }

        throw new InvalidOperationException(
            $"Failed to acquire discovery lock for namespace {namespaceId} after {MaxRetries} attempts (ETag race exhausted).");
    }

    public async Task ReleaseAsync(string namespaceId, string runId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        // Only release if WE are the holder. Read → compare → conditional clear.
        try
        {
            var response = await _container.ReadItemAsync<DiscoveryLockDocument>(
                LockDocumentId, new PartitionKey(namespaceId), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!string.Equals(response.Resource.CurrentRunId, runId, StringComparison.Ordinal))
            {
                // We're not the holder anymore — someone stole or already released. Nothing to do.
                return;
            }

            var cleared = response.Resource with
            {
                CurrentRunId = null,
                AcquiredByPodId = null,
                ExpectedReleaseByUtc = null,
            };
            await _container.ReplaceItemAsync(
                cleared, LockDocumentId, new PartitionKey(namespaceId),
                requestOptions: new ItemRequestOptions { IfMatchEtag = response.ETag },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.PreconditionFailed)
        {
            // Already released or concurrently modified — idempotent path.
        }
    }

    private async Task<bool> TryReplaceWithIfMatchAsync(
        DiscoveryLockDocument doc,
        PartitionKey partitionKey,
        string ifMatch,
        CancellationToken cancellationToken)
    {
        try
        {
            await _container.ReplaceItemAsync(
                doc, LockDocumentId, partitionKey,
                requestOptions: new ItemRequestOptions { IfMatchEtag = ifMatch },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            return false;
        }
    }

    private DiscoveryLockDocument NewLockDocument(string namespaceId, string runId, string podId)
    {
        var now = _time.GetUtcNow();
        return new DiscoveryLockDocument
        {
            Id = LockDocumentId,
            SchemaVersion = "1.0",
            NamespaceId = namespaceId,
            CurrentRunId = runId,
            AcquiredUtc = now,
            AcquiredByPodId = podId,
            ExpectedReleaseByUtc = now.AddSeconds(_expirySeconds),
        };
    }
}

internal sealed record DiscoveryLockDocument
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("schemaVersion")] public required string SchemaVersion { get; init; }
    [JsonPropertyName("namespaceId")] public required string NamespaceId { get; init; }
    [JsonPropertyName("currentRunId")] public string? CurrentRunId { get; init; }
    [JsonPropertyName("acquiredUtc")] public DateTimeOffset? AcquiredUtc { get; init; }
    [JsonPropertyName("acquiredByPodId")] public string? AcquiredByPodId { get; init; }
    [JsonPropertyName("expectedReleaseByUtc")] public DateTimeOffset? ExpectedReleaseByUtc { get; init; }
}
