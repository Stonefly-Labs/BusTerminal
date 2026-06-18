// The batcher catches Exception in the drain loop so a per-entity write
// failure cannot kill the channel and stall the run. The catch IS the
// retry/observability boundary — CA1031 suppression is intentional.
#pragma warning disable CA1031

using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using BusTerminal.Indexer.Discovery.Classification;
using BusTerminal.Indexer.Discovery.Telemetry;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Indexer.Discovery.Persistence;

// Spec 009 / T052 + R-05. Channel-backed write pipeline. The orchestrator
// pushes DiscoveryUpsert records on the input side; up to 32 worker tasks
// drain the channel and call IPublishedEntityWriter.UpsertAzureSourcedAsync.
// Classification counts are aggregated as the writes complete.
//
// Memory bound: the channel has a fixed capacity of 256 items. The fetch
// pipeline blocks when full so the worker never materializes the entire
// namespace's worth of entities at once (constitution: ≤ 4 GB memory).
public sealed partial class DiscoveryWriteBatcher : IAsyncDisposable
{
    public const int DefaultParallelism = 32;
    public const int DefaultChannelCapacity = 256;

    private readonly IPublishedEntityWriter _writer;
    private readonly Channel<DiscoveryUpsert> _channel;
    private readonly List<Task> _workers;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly ILogger<DiscoveryWriteBatcher> _logger;
    private readonly Lock _countsLock = new();

    private int _newCount;
    private int _updatedCount;
    private int _unchangedCount;

    public DiscoveryWriteBatcher(
        IPublishedEntityWriter writer,
        ILogger<DiscoveryWriteBatcher> logger,
        int parallelism = DefaultParallelism,
        int channelCapacity = DefaultChannelCapacity)
    {
        _writer = writer;
        _logger = logger;
        _channel = Channel.CreateBounded<DiscoveryUpsert>(new BoundedChannelOptions(channelCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
        _shutdownCts = new CancellationTokenSource();
        _workers = new List<Task>(parallelism);
        for (var i = 0; i < parallelism; i++)
        {
            _workers.Add(Task.Run(DrainAsync));
        }
    }

    public ValueTask EnqueueAsync(DiscoveryUpsert upsert, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(upsert, cancellationToken);

    public async Task CompleteAsync()
    {
        _channel.Writer.Complete();
        await Task.WhenAll(_workers).ConfigureAwait(false);
    }

    public BatchCounts SnapshotCounts()
    {
        lock (_countsLock)
        {
            return new BatchCounts(_newCount, _updatedCount, _unchangedCount);
        }
    }

    private async Task DrainAsync()
    {
        var reader = _channel.Reader;
        while (await reader.WaitToReadAsync(_shutdownCts.Token).ConfigureAwait(false))
        {
            while (reader.TryRead(out var item))
            {
                try
                {
                    using var span = DiscoveryActivitySource.Instance.StartActivity(
                        DiscoveryActivitySource.SpanNames.PersistBatch, ActivityKind.Client);
                    var outcome = await _writer.UpsertAzureSourcedAsync(item, _shutdownCts.Token).ConfigureAwait(false);
                    span?.SetTag(DiscoveryActivitySource.AttributeKeys.PersistBatchSize, 1);
                    span?.SetTag(DiscoveryActivitySource.AttributeKeys.PersistRuConsumed, outcome.RuConsumed);
                    lock (_countsLock)
                    {
                        switch (outcome.Outcome)
                        {
                            case ClassificationOutcome.New: _newCount++; break;
                            case ClassificationOutcome.Updated: _updatedCount++; break;
                            case ClassificationOutcome.Unchanged: _unchangedCount++; break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown.
                    return;
                }
                catch (Exception ex)
                {
                    // A persistence failure on a single entity should not kill
                    // the batcher — log it and move on. The classification
                    // counts will be slightly off; the orchestrator's final
                    // counts include only successful writes.
                    LogWriteFailed(item.EntityId, ex.Message);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_shutdownCts.IsCancellationRequested)
        {
            _shutdownCts.Cancel();
        }
        try
        {
            await Task.WhenAll(_workers).ConfigureAwait(false);
        }
        catch
        {
            // Workers may have observed cancellation already.
        }
        _shutdownCts.Dispose();
    }

    [LoggerMessage(EventId = 9901, Level = LogLevel.Warning,
        Message = "Discovery upsert failed entity={EntityId} reason={Reason}.")]
    private partial void LogWriteFailed(string entityId, string reason);
}

public sealed record BatchCounts(int NewCount, int UpdatedCount, int UnchangedCount);

// Spec 009 / R-07. Worker-side stable identity computer. Duplicates the
// API-side PublishedEntityIdComputer so the two emitters cannot diverge.
public static class PublishedEntityIdComputer
{
    public const string IdPrefix = "pe_";
    private static readonly char[] Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    public static string ComputeFromCompositeKey(string compositeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compositeKey);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(compositeKey), hash);
        return IdPrefix + EncodeBase32Truncated(hash, 24);
    }

    private static string EncodeBase32Truncated(ReadOnlySpan<byte> bytes, int targetChars)
    {
        const int bitsPerChar = 5;
        var sb = new StringBuilder(targetChars);
        ulong buffer = 0;
        var bitsInBuffer = 0;
        var charsEmitted = 0;
        var byteIndex = 0;
        var bytesNeeded = (targetChars * bitsPerChar + 7) / 8;
        while (charsEmitted < targetChars)
        {
            while (bitsInBuffer < bitsPerChar && byteIndex < bytesNeeded)
            {
                buffer = (buffer << 8) | bytes[byteIndex];
                bitsInBuffer += 8;
                byteIndex++;
            }
            var shift = bitsInBuffer - bitsPerChar;
            var index = (int)((buffer >> shift) & 0x1F);
            sb.Append(Base32Alphabet[index]);
            buffer &= (1UL << shift) - 1;
            bitsInBuffer = shift;
            charsEmitted++;
        }
        return sb.ToString();
    }
}
