using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Tests.Features.Registry._Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery._Shared.Persistence;

// Spec 009 / T019 — CosmosDiscoveryRunStore CRUD over the discovery-runs
// container. Runs against the dev Cosmos account via the shared
// RegistryFixture; skipped when BUSTERMINAL_TEST_COSMOS_ENDPOINT is unset.
[Collection("RegistryFixture")]
[Trait("Category", "Integration")]
public sealed class DiscoveryRunStoreTests
{
    private readonly RegistryFixture _fixture;

    public DiscoveryRunStoreTests(RegistryFixture fixture)
    {
        _fixture = fixture;
    }

    private CosmosDiscoveryRunStore CreateStore()
    {
        var options = Options.Create(new CosmosRegistryOptions
        {
            Database = "canonical",
            EntitiesContainer = "registry-entities",
            AuditContainer = "registry-audit",
            LeasesContainer = "registry-entities-leases",
            ValidationRunsContainer = "namespace-validation-runs",
            DiscoveryRunsContainer = "discovery-runs",
            DiscoveryLocksContainer = "discovery-locks",
        });
        return new CosmosDiscoveryRunStore(
            _fixture.Client, options, NullLogger<CosmosDiscoveryRunStore>.Instance);
    }

    private static DiscoveryRun NewRun(string namespaceId, DateTimeOffset startedUtc, DiscoveryRunStatus status = DiscoveryRunStatus.Queued) => new(
        Id: $"dr_{Guid.NewGuid():N}"[..18],
        SchemaVersion: DiscoveryRun.CurrentSchemaVersion,
        NamespaceId: namespaceId,
        Status: status,
        Trigger: DiscoveryTrigger.Manual,
        StartedUtc: startedUtc,
        CompletedUtc: null,
        DurationMs: null,
        RequestedBy: "user-test",
        QueueCount: 0, TopicCount: 0, SubscriptionCount: 0, RuleCount: 0,
        NewCount: 0, UpdatedCount: 0, UnchangedCount: 0, MissingCount: 0,
        Failure: null,
        CoalescedRequests: Array.Empty<CoalescedRequest>(),
        CorrelationId: "00-test-trace-id-01");

    [Fact]
    public async Task Create_Then_Get_RoundTripsDocument()
    {
        if (!_fixture.ShouldRun()) return;
        var store = CreateStore();
        var ns = $"ns_test_{Guid.NewGuid():N}";
        var run = NewRun(ns, DateTimeOffset.UtcNow);

        var created = await store.CreateAsync(run, CancellationToken.None);
        var read = await store.GetAsync(run.Id, ns, CancellationToken.None);

        created.Id.Should().Be(run.Id);
        read.Should().NotBeNull();
        read!.Status.Should().Be(DiscoveryRunStatus.Queued);
    }

    [Fact]
    public async Task UpdateStatus_TransitionsToTerminal_AndPersistsCounts()
    {
        if (!_fixture.ShouldRun()) return;
        var store = CreateStore();
        var ns = $"ns_test_{Guid.NewGuid():N}";
        var run = NewRun(ns, DateTimeOffset.UtcNow);
        await store.CreateAsync(run, CancellationToken.None);

        var updated = await store.UpdateStatusAsync(run.Id, ns, new DiscoveryRunStatusUpdate(
            Status: DiscoveryRunStatus.Succeeded,
            CompletedUtc: DateTimeOffset.UtcNow,
            DurationMs: 12345,
            NewCount: 3,
            UpdatedCount: 1,
            UnchangedCount: 12,
            MissingCount: 0), ifMatch: null, CancellationToken.None);

        updated.Status.Should().Be(DiscoveryRunStatus.Succeeded);
        updated.NewCount.Should().Be(3);
        updated.DurationMs.Should().Be(12345);
    }

    [Fact]
    public async Task UpdateStatus_WithFailure_PersistsFailureRecord()
    {
        if (!_fixture.ShouldRun()) return;
        var store = CreateStore();
        var ns = $"ns_test_{Guid.NewGuid():N}";
        var run = NewRun(ns, DateTimeOffset.UtcNow);
        await store.CreateAsync(run, CancellationToken.None);

        var failure = new DiscoveryRunFailure(
            DiscoveryFailureCategory.Throttled,
            "ARM 429 (retries exhausted)",
            DiscoveryPhase.FetchSubscriptions,
            RetriesExhausted: 3);

        var updated = await store.UpdateStatusAsync(run.Id, ns, new DiscoveryRunStatusUpdate(
            Status: DiscoveryRunStatus.Failed,
            CompletedUtc: DateTimeOffset.UtcNow,
            Failure: failure), ifMatch: null, CancellationToken.None);

        updated.Status.Should().Be(DiscoveryRunStatus.Failed);
        updated.Failure.Should().NotBeNull();
        updated.Failure!.Category.Should().Be(DiscoveryFailureCategory.Throttled);
    }

    [Fact]
    public async Task ListByNamespace_ReturnsReverseChronological()
    {
        if (!_fixture.ShouldRun()) return;
        var store = CreateStore();
        var ns = $"ns_test_{Guid.NewGuid():N}";
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-5);
        var older = NewRun(ns, t0);
        var newer = NewRun(ns, t0.AddMinutes(1));

        await store.CreateAsync(older, CancellationToken.None);
        await store.CreateAsync(newer, CancellationToken.None);

        var page = await store.ListByNamespaceAsync(ns, pageSize: 10, continuationToken: null, CancellationToken.None);
        page.Items.Should().HaveCount(2);
        page.Items[0].Id.Should().Be(newer.Id);
        page.Items[1].Id.Should().Be(older.Id);
    }

    [Fact]
    public async Task AppendCoalescedRequest_AppendsToAuditArray()
    {
        if (!_fixture.ShouldRun()) return;
        var store = CreateStore();
        var ns = $"ns_test_{Guid.NewGuid():N}";
        var run = NewRun(ns, DateTimeOffset.UtcNow);
        await store.CreateAsync(run, CancellationToken.None);

        await store.AppendCoalescedRequestAsync(run.Id, ns,
            new CoalescedRequest(DateTimeOffset.UtcNow, "user-coalesce-1"), CancellationToken.None);

        var read = await store.GetAsync(run.Id, ns, CancellationToken.None);
        read!.CoalescedRequests.Should().ContainSingle(r => r.RequestedBy == "user-coalesce-1");
    }
}
