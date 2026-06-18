using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BusTerminal.Indexer.Discovery;
using BusTerminal.Indexer.Discovery.Classification;
using BusTerminal.Indexer.Discovery.Persistence;
using BusTerminal.Indexer.Discovery.Providers;
using BusTerminal.Indexer.Discovery.Telemetry;
using FluentAssertions;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BusTerminal.Indexer.Tests.Discovery;

// Spec 009 / T037. Orchestrator end-to-end behavior over an in-memory
// provider + writer. Covers:
//   - empty namespace → Succeeded with all counts at zero
//   - full namespace  → counts match input; New classification recorded
//   - idempotent re-run → existing entities classified as Unchanged
//   - missing-sweep  → previously-Active entities not seen by run flip to Missing
//   - partial-failure → topics-throw → run Failed, no Missing transitions for
//                       successfully-completed scopes
public sealed class EntityDiscoveryOrchestratorTests
{
    private static EntityDiscoveryProviderContext NewContext() => new(
        NamespaceId: "ns_test_1",
        AzureSubscriptionId: "00000000-0000-0000-0000-000000000001",
        ResourceGroup: "rg-test",
        NamespaceName: "ns-test");

    [Fact]
    public async Task EmptyNamespace_CompletesAsSucceeded_WithZeroCounts()
    {
        var provider = new FakeProvider();
        var writer = new FakeWriter();
        var runUpdater = new FakeRunUpdater();
        var resolver = new StubResolver();
        var sut = NewOrchestrator(provider, writer, runUpdater, resolver);

        await sut.RunAsync(NewRequest(), CancellationToken.None);

        runUpdater.Succeeded.Should().BeTrue();
        runUpdater.Failed.Should().BeFalse();
        runUpdater.SuccessCounts!.QueueCount.Should().Be(0);
        runUpdater.SuccessCounts!.TopicCount.Should().Be(0);
        runUpdater.SuccessCounts!.NewCount.Should().Be(0);
    }

    [Fact]
    public async Task FullNamespace_FirstRun_AllClassifiedAsNew()
    {
        var provider = new FakeProvider();
        provider.Queues.Add(MakeEntity(DiscoveredEntityType.Queue, "orders-inbox"));
        provider.Queues.Add(MakeEntity(DiscoveredEntityType.Queue, "payments-inbox"));
        provider.Topics.Add(MakeEntity(DiscoveredEntityType.Topic, "events"));
        provider.SubsAndRules.Add(MakeEntity(DiscoveredEntityType.Subscription, "fulfillment", parent: "t:ns_test_1/events"));

        var writer = new FakeWriter();
        var runUpdater = new FakeRunUpdater();
        var resolver = new StubResolver();
        var sut = NewOrchestrator(provider, writer, runUpdater, resolver);

        await sut.RunAsync(NewRequest(), CancellationToken.None);

        runUpdater.Succeeded.Should().BeTrue();
        runUpdater.SuccessCounts!.QueueCount.Should().Be(2);
        runUpdater.SuccessCounts.TopicCount.Should().Be(1);
        runUpdater.SuccessCounts.SubscriptionCount.Should().Be(1);
        runUpdater.SuccessCounts.NewCount.Should().Be(4);
    }

    [Fact]
    public async Task SecondRun_OnUnchangedEntities_ClassifiesAsUnchanged()
    {
        var provider = new FakeProvider();
        provider.Queues.Add(MakeEntity(DiscoveredEntityType.Queue, "orders-inbox"));

        var writer = new FakeWriter();
        var runUpdater = new FakeRunUpdater();
        var resolver = new StubResolver();

        var sut = NewOrchestrator(provider, writer, runUpdater, resolver);
        await sut.RunAsync(NewRequest("dr_001"), CancellationToken.None);

        // Reset the run updater + run again with the same provider snapshot.
        runUpdater.Reset();
        await sut.RunAsync(NewRequest("dr_002"), CancellationToken.None);

        runUpdater.Succeeded.Should().BeTrue();
        runUpdater.SuccessCounts!.NewCount.Should().Be(0);
        runUpdater.SuccessCounts.UnchangedCount.Should().Be(1);
    }

    [Fact]
    public async Task MissingSweep_FlipsActiveButUnseen_ToMissing()
    {
        var provider = new FakeProvider();
        provider.Queues.Add(MakeEntity(DiscoveredEntityType.Queue, "orders-inbox"));

        var writer = new FakeWriter();
        // Seed an entity that the provider does NOT yield this run.
        writer.SeedExisting("pe_legacy", new FakeWriter.SeededEntity(
            Environment: "dev",
            NamespaceId: "ns_test_1",
            AzureSourcedHash: "sha256:legacy",
            LastSeenUtc: DateTimeOffset.UtcNow.AddDays(-1)));

        var runUpdater = new FakeRunUpdater();
        var resolver = new StubResolver();
        var sut = NewOrchestrator(provider, writer, runUpdater, resolver);

        await sut.RunAsync(NewRequest(), CancellationToken.None);

        runUpdater.Succeeded.Should().BeTrue();
        runUpdater.SuccessCounts!.MissingCount.Should().Be(1);
        writer.MissingTransitions.Should().Contain("pe_legacy");
    }

    [Fact]
    public async Task PartialFailure_TopicsThrows_RunMarkedFailed_NoMissingSweep()
    {
        var provider = new FakeProvider { ThrowOnTopics = new InvalidOperationException("topics fetch boom") };
        provider.Queues.Add(MakeEntity(DiscoveredEntityType.Queue, "orders-inbox"));

        var writer = new FakeWriter();
        writer.SeedExisting("pe_should_stay_active", new FakeWriter.SeededEntity(
            Environment: "dev", NamespaceId: "ns_test_1",
            AzureSourcedHash: "sha256:abc", LastSeenUtc: DateTimeOffset.UtcNow.AddDays(-1)));

        var runUpdater = new FakeRunUpdater();
        var resolver = new StubResolver();
        var sut = NewOrchestrator(provider, writer, runUpdater, resolver);

        await sut.RunAsync(NewRequest(), CancellationToken.None);

        runUpdater.Failed.Should().BeTrue();
        runUpdater.FailureRecord!.OccurredAtPhase.Should().Be("FetchTopics");
        // Crucial — the partial-failure invariant: no Missing transitions
        // when a scope failed to complete.
        writer.MissingTransitions.Should().BeEmpty();
    }

    [Fact]
    public async Task NamespaceContextNotFound_RecordsNotFoundFailure()
    {
        var provider = new FakeProvider();
        var writer = new FakeWriter();
        var runUpdater = new FakeRunUpdater();
        var resolver = new StubResolver { ThrowOnResolve = new InvalidOperationException("Namespace ns_test_1 not found.") };
        var sut = NewOrchestrator(provider, writer, runUpdater, resolver);

        await sut.RunAsync(NewRequest(), CancellationToken.None);

        runUpdater.Failed.Should().BeTrue();
        runUpdater.FailureRecord!.Category.Should().Be("NotFound");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static DiscoveryRunRequest NewRequest(string runId = "dr_test_001") => new(
        DiscoveryRunId: runId,
        NamespaceId: "ns_test_1",
        Environment: "dev",
        RequestedBy: "00000000-0000-0000-0000-000000000099",
        CorrelationId: "00-trace-id-01");

    private static DiscoveredEntity MakeEntity(DiscoveredEntityType type, string name, string? parent = null)
    {
        var ns = "ns_test_1";
        var compositeKey = type switch
        {
            DiscoveredEntityType.Queue => $"q:{ns}/{name}",
            DiscoveredEntityType.Topic => $"t:{ns}/{name}",
            DiscoveredEntityType.Subscription => $"s:{ns}/events/{name}",
            DiscoveredEntityType.Rule => $"r:{ns}/events/sub-x/{name}",
            _ => $"x:{ns}/{name}",
        };
        return new DiscoveredEntity(
            EntityType: type,
            NamespaceId: ns,
            Name: name,
            CompositeKey: compositeKey,
            ParentCompositeKey: parent,
            AzureSourced: new Dictionary<string, object?>
            {
                ["$type"] = type.ToString(),
                ["status"] = "Active",
            });
    }

    private static EntityDiscoveryOrchestrator NewOrchestrator(
        IEntityDiscoveryProvider provider,
        IPublishedEntityWriter writer,
        IDiscoveryRunUpdater runUpdater,
        INamespaceContextResolver resolver)
    {
        using var meterFactory = new TestMeterFactory();
        return new EntityDiscoveryOrchestrator(
            provider, writer, runUpdater, resolver,
            new DiscoveryMeter(meterFactory),
            TimeProvider.System,
            NullLogger<EntityDiscoveryOrchestrator>.Instance);
    }

    private sealed class FakeProvider : IEntityDiscoveryProvider
    {
        public List<DiscoveredEntity> Queues { get; } = new();
        public List<DiscoveredEntity> Topics { get; } = new();
        public List<DiscoveredEntity> SubsAndRules { get; } = new();
        public Exception? ThrowOnTopics { get; set; }

        public IAsyncEnumerable<DiscoveredEntity> StreamQueuesAsync(EntityDiscoveryProviderContext context, CancellationToken cancellationToken)
            => ToAsync(Queues, null, cancellationToken);

        public IAsyncEnumerable<DiscoveredEntity> StreamTopicsAsync(EntityDiscoveryProviderContext context, CancellationToken cancellationToken)
            => ToAsync(Topics, ThrowOnTopics, cancellationToken);

        public IAsyncEnumerable<DiscoveredEntity> StreamSubscriptionsAndRulesAsync(EntityDiscoveryProviderContext context, CancellationToken cancellationToken)
            => ToAsync(SubsAndRules, null, cancellationToken);

        private static async IAsyncEnumerable<DiscoveredEntity> ToAsync(IReadOnlyList<DiscoveredEntity> items, Exception? throwAt, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (throwAt is not null) throw throwAt;
            foreach (var e in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return e;
                await Task.Yield();
            }
        }
    }

    private sealed class FakeWriter : IPublishedEntityWriter
    {
        private readonly ConcurrentDictionary<string, SeededEntity> _seeded = new();
        private readonly ConcurrentDictionary<string, byte> _seenThisRun = new();
        public List<string> MissingTransitions { get; } = new();
        public sealed record SeededEntity(string Environment, string NamespaceId, string AzureSourcedHash, DateTimeOffset LastSeenUtc);

        public void SeedExisting(string id, SeededEntity entity)
        {
            _seeded[id] = entity;
        }

        public Task<UpsertOutcome> UpsertAzureSourcedAsync(DiscoveryUpsert upsert, CancellationToken cancellationToken)
        {
            _seenThisRun[upsert.EntityId] = 1;
            var outcome = _seeded.TryGetValue(upsert.EntityId, out var existing)
                ? (existing.AzureSourcedHash == upsert.AzureSourcedHash ? ClassificationOutcome.Unchanged : ClassificationOutcome.Updated)
                : ClassificationOutcome.New;
            _seeded[upsert.EntityId] = new SeededEntity(upsert.Environment, upsert.NamespaceId, upsert.AzureSourcedHash, upsert.DiscoveryRunStartedUtc);
            return Task.FromResult(new UpsertOutcome(outcome, RuConsumed: 1.0));
        }

        public async IAsyncEnumerable<MissingCandidate> ListMissingCandidatesAsync(string namespaceId, string environment, DateTimeOffset olderThan, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            foreach (var kv in _seeded)
            {
                if (kv.Value.NamespaceId != namespaceId) continue;
                if (_seenThisRun.ContainsKey(kv.Key)) continue;
                if (kv.Value.LastSeenUtc >= olderThan) continue;
                yield return new MissingCandidate(kv.Key, kv.Value.Environment, ETag: "etag-x");
            }
        }

        public Task TransitionToMissingAsync(string entityId, string environment, string ifMatch, DateTimeOffset whenUtc, string runId, CancellationToken cancellationToken)
        {
            MissingTransitions.Add(entityId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRunUpdater : IDiscoveryRunUpdater
    {
        public bool InProgress { get; private set; }
        public bool Succeeded { get; private set; }
        public bool Failed { get; private set; }
        public RunOutcomeCounts? SuccessCounts { get; private set; }
        public RunFailureRecord? FailureRecord { get; private set; }

        public void Reset()
        {
            InProgress = false; Succeeded = false; Failed = false;
            SuccessCounts = null; FailureRecord = null;
        }

        public Task TransitionToInProgressAsync(string runId, string namespaceId, DateTimeOffset whenUtc, CancellationToken cancellationToken)
        {
            InProgress = true;
            return Task.CompletedTask;
        }

        public Task RecordSuccessAsync(string runId, string namespaceId, RunOutcomeCounts counts, DateTimeOffset completedUtc, int durationMs, CancellationToken cancellationToken)
        {
            Succeeded = true;
            SuccessCounts = counts;
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(string runId, string namespaceId, RunFailureRecord failure, DateTimeOffset completedUtc, int durationMs, CancellationToken cancellationToken)
        {
            Failed = true;
            FailureRecord = failure;
            return Task.CompletedTask;
        }
    }

    private sealed class StubResolver : INamespaceContextResolver
    {
        public Exception? ThrowOnResolve { get; set; }
        public Task<NamespaceDiscoveryContext> ResolveAsync(string namespaceId, CancellationToken cancellationToken)
        {
            if (ThrowOnResolve is not null) throw ThrowOnResolve;
            return Task.FromResult(new NamespaceDiscoveryContext(
                AzureSubscriptionId: "00000000-0000-0000-0000-000000000001",
                ResourceGroup: "rg-test",
                NamespaceName: "ns-test",
                Environment: "dev"));
        }
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<System.Diagnostics.Metrics.Meter> _meters = new();
        public System.Diagnostics.Metrics.Meter Create(MeterOptions options)
        {
            var m = new System.Diagnostics.Metrics.Meter(options.Name, options.Version);
            _meters.Add(m);
            return m;
        }
        public void Dispose()
        {
            foreach (var m in _meters) m.Dispose();
        }
    }
}
