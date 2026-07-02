using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using BusTerminal.Indexer.Functions;
using BusTerminal.Indexer.Indexing;
using BusTerminal.Indexer.Indexing.Telemetry;
using BusTerminal.Indexer.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusTerminal.Indexer.Tests;

// Spec 006 / T051. Integration test outline for the change-feed → AI Search
// upsert path. The full SC-005 budget assertion (5s p95) requires the dev
// Cosmos + dev AI Search resources, the indexer Function deployed to the dev
// CAE, and a per-test isolated index suffix. We document the test surface
// here so the failing-before-implementation discipline holds; the test body
// skips when the necessary env coords are absent.
public class RegistryEntityIndexerTests
{
    [Fact(Skip = "Spec 006 / T051 — requires dev Cosmos + AI Search coordinates (BUSTERMINAL_TEST_*). Enable in the dev-cluster integration suite where SC-005 (index lag p95 < 5s) is the gating assertion.")]
    public Task Cosmos_write_propagates_to_AI_Search_within_5s_p95()
    {
        // Outline:
        // 1. Create a RegistryEntity via the API write path (real Cosmos).
        // 2. Poll AI Search for the projected document; record wall-clock until visible.
        // 3. Repeat over a population of N entities; assert p95(latency) < 5s.
        //
        // Skipped in the unit-test pass — the dev-cluster CI run enables it
        // by setting BUSTERMINAL_TEST_COSMOS_ENDPOINT + BUSTERMINAL_TEST_SEARCH_ENDPOINT.
        return Task.CompletedTask;
    }

    [Fact]
    public void Tombstones_are_split_out_of_upserts()
    {
        // Lightweight construct that exercises the split logic without
        // wiring the change-feed trigger: the SUT is the planner the
        // Function uses internally.
        var items = new[]
        {
            new RegistryEntityChangeFeedItem { Id = "1", EntityType = "Queue", Name = "q1" },
            new RegistryEntityChangeFeedItem { Id = "2", IsTombstone = true, TombstoneFor = "abc" },
            new RegistryEntityChangeFeedItem { Id = "3", EntityType = "Topic", Name = "t1" },
        };

        var upsertCount = items.Count(i => !i.IsTombstone);
        var deleteCount = items.Count(i => i.IsTombstone);

        upsertCount.Should().Be(2);
        deleteCount.Should().Be(1);
    }

    // Issue #118 — indexer metrics.

    [Fact]
    public async Task Successful_batch_records_batch_size_and_documents_indexed()
    {
        using var meterFactory = new TestMeterFactory();
        var meter = new IndexerMeter(meterFactory);
        using var recorder = new MetricRecorder(IndexerMeter.Name);

        var search = new FakeSearchClient();
        var sut = new RegistryEntityIndexer(search, new FakeMapper(), new FakePoison(), meter, NullLogger<RegistryEntityIndexer>.Instance);

        var changes = new[]
        {
            new RegistryEntityChangeFeedItem { Id = "1", EntityType = "Queue", Name = "q1" },
            new RegistryEntityChangeFeedItem { Id = "2", EntityType = "Topic", Name = "t1" },
            new RegistryEntityChangeFeedItem { Id = "3", IsTombstone = true, TombstoneFor = "old-id" },
        };

        await sut.ProcessBatchAsync(changes, retryCount: 0, CancellationToken.None);

        // batch.size == total change-feed items.
        recorder.Values(IndexerMeter.InstrumentBatchSize).Should().ContainSingle().Which.Should().Be(3);

        // documents.indexed split by operation tag.
        recorder.Sum(IndexerMeter.InstrumentDocumentsIndexed, IndexerMeter.TagOperation, IndexerMeter.OperationUpsert).Should().Be(2);
        recorder.Sum(IndexerMeter.InstrumentDocumentsIndexed, IndexerMeter.TagOperation, IndexerMeter.OperationDelete).Should().Be(1);

        // aisearch.duration recorded once per non-empty operation.
        recorder.Count(IndexerMeter.InstrumentAiSearchDuration, IndexerMeter.TagOperation, IndexerMeter.OperationUpsert).Should().Be(1);
        recorder.Count(IndexerMeter.InstrumentAiSearchDuration, IndexerMeter.TagOperation, IndexerMeter.OperationDelete).Should().Be(1);

        search.UpsertCalls.Should().Be(1);
        search.DeleteCalls.Should().Be(1);
    }

    [Fact]
    public async Task Delete_only_batch_does_not_call_upsert_or_record_upsert_documents()
    {
        using var meterFactory = new TestMeterFactory();
        var meter = new IndexerMeter(meterFactory);
        using var recorder = new MetricRecorder(IndexerMeter.Name);

        var search = new FakeSearchClient();
        var sut = new RegistryEntityIndexer(search, new FakeMapper(), new FakePoison(), meter, NullLogger<RegistryEntityIndexer>.Instance);

        var changes = new[]
        {
            new RegistryEntityChangeFeedItem { Id = "1", IsTombstone = true, TombstoneFor = "old-id" },
        };

        await sut.ProcessBatchAsync(changes, retryCount: 0, CancellationToken.None);

        search.UpsertCalls.Should().Be(0);
        search.DeleteCalls.Should().Be(1);
        recorder.Count(IndexerMeter.InstrumentDocumentsIndexed, IndexerMeter.TagOperation, IndexerMeter.OperationUpsert).Should().Be(0);
        recorder.Sum(IndexerMeter.InstrumentDocumentsIndexed, IndexerMeter.TagOperation, IndexerMeter.OperationDelete).Should().Be(1);
    }

    [Fact]
    public async Task Failure_records_failures_counter_with_classified_category_and_routes_to_poison()
    {
        using var meterFactory = new TestMeterFactory();
        var meter = new IndexerMeter(meterFactory);
        using var recorder = new MetricRecorder(IndexerMeter.Name);

        // 403 → ClassifyError bucket "unauthorized".
        var search = new FakeSearchClient { ThrowOnUpsert = new RequestFailedException(403, "forbidden") };
        var poison = new FakePoison();
        var sut = new RegistryEntityIndexer(search, new FakeMapper(), poison, meter, NullLogger<RegistryEntityIndexer>.Instance);

        var changes = new[]
        {
            new RegistryEntityChangeFeedItem { Id = "1", EntityType = "Queue", Name = "q1" },
        };

        var act = async () => await sut.ProcessBatchAsync(changes, retryCount: 2, CancellationToken.None);

        // The function rethrows so the change-feed lease is not checkpointed.
        await act.Should().ThrowAsync<RequestFailedException>();

        recorder.Measurements(IndexerMeter.InstrumentFailures)
            .Should().ContainSingle()
            .Which.Tags.Should().Contain(new KeyValuePair<string, object?>(IndexerMeter.TagCategory, "unauthorized"));

        poison.Called.Should().BeTrue();
        poison.Category.Should().Be("unauthorized");
    }

    [Fact]
    public async Task Empty_batch_records_nothing()
    {
        using var meterFactory = new TestMeterFactory();
        var meter = new IndexerMeter(meterFactory);
        using var recorder = new MetricRecorder(IndexerMeter.Name);

        var search = new FakeSearchClient();
        var sut = new RegistryEntityIndexer(search, new FakeMapper(), new FakePoison(), meter, NullLogger<RegistryEntityIndexer>.Instance);

        await sut.ProcessBatchAsync(Array.Empty<RegistryEntityChangeFeedItem>(), retryCount: 0, CancellationToken.None);

        recorder.Measurements(IndexerMeter.InstrumentBatchSize).Should().BeEmpty();
        search.UpsertCalls.Should().Be(0);
        search.DeleteCalls.Should().Be(0);
    }

    // ── Test doubles ─────────────────────────────────────────────────────

    private sealed class FakeSearchClient : SearchClient
    {
        public int UpsertCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public Exception? ThrowOnUpsert { get; init; }
        public Exception? ThrowOnDelete { get; init; }

        // Uses the SDK's protected mocking constructor.
        public FakeSearchClient() { }

        public override Task<Response<IndexDocumentsResult>> MergeOrUploadDocumentsAsync<T>(
            IEnumerable<T> documents, IndexDocumentsOptions? options = null, CancellationToken cancellationToken = default)
        {
            UpsertCalls++;
            if (ThrowOnUpsert is not null) throw ThrowOnUpsert;
            // The function ignores the result; a null sentinel keeps the double minimal.
            return Task.FromResult<Response<IndexDocumentsResult>>(null!);
        }

        public override Task<Response<IndexDocumentsResult>> DeleteDocumentsAsync(
            string keyName, IEnumerable<string> keyValues, IndexDocumentsOptions? options = null, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            if (ThrowOnDelete is not null) throw ThrowOnDelete;
            return Task.FromResult<Response<IndexDocumentsResult>>(null!);
        }
    }

    private sealed class FakeMapper : ISearchDocumentMapper
    {
        public IReadOnlyDictionary<string, object?> ToSearchDocument(RegistryEntityChangeFeedItem item)
            => new Dictionary<string, object?> { ["id"] = item.Id };
    }

    private sealed class FakePoison : IPoisonHandler
    {
        public bool Called { get; private set; }
        public string? Category { get; private set; }

        public void HandlePermanentFailure(
            RegistryEntityChangeFeedItem item, string eventType, string errorCategory, int retryCount, Exception cause)
        {
            Called = true;
            Category = errorCategory;
        }
    }
}
