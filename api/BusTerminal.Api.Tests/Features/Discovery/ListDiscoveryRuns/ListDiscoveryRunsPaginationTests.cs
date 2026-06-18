using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Persistence;
using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Tests.Features.Registry._Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.ListDiscoveryRuns;

// Spec 009 / T081 / US3. Integration coverage for continuation-token paging
// over CosmosDiscoveryRunStore.ListByNamespaceAsync against the dev Cosmos
// emulator. Seeds N > pageSize runs in a fresh namespace then walks the
// cursor to terminal, asserting:
//   - each page is ≤ pageSize
//   - the union of pages contains every seeded run exactly once
//   - the walk terminates (no infinite cursor loop)
//   - results are emitted in reverse-chronological order across pages
[Collection("RegistryFixture")]
[Trait("Category", "Integration")]
public sealed class ListDiscoveryRunsPaginationTests
{
    private readonly RegistryFixture _fixture;

    public ListDiscoveryRunsPaginationTests(RegistryFixture fixture)
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

    [Fact]
    public async Task ListByNamespace_WalksContinuationToken_AcrossSevenSeededRuns()
    {
        if (!_fixture.ShouldRun()) return;

        var store = CreateStore();
        var ns = $"ns_paging_{Guid.NewGuid():N}";
        var seeded = await SeedRunsAsync(store, ns, count: 7);

        const int pageSize = 3;
        var collected = new List<string>();
        string? token = null;
        var safetyHops = 0;
        var pageStartedTimes = new List<DateTimeOffset>();

        do
        {
            var page = await store.ListByNamespaceAsync(ns, pageSize, token, CancellationToken.None);
            page.Items.Count.Should().BeGreaterThan(0);
            page.Items.Count.Should().BeLessThanOrEqualTo(pageSize);
            foreach (var run in page.Items)
            {
                collected.Add(run.Id);
                pageStartedTimes.Add(run.StartedUtc);
            }
            token = page.ContinuationToken;
            safetyHops++;
        } while (token is not null && safetyHops < 10);

        // Termination: at most ceil(7/3) = 3 pages.
        safetyHops.Should().BeLessThanOrEqualTo(3);

        collected.Should().HaveCount(7);
        collected.Distinct().Should().HaveCount(7);
        collected.Should().BeEquivalentTo(seeded.Select(r => r.Id));

        // Reverse-chronological across the entire walk.
        pageStartedTimes.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task ListByNamespace_PageSizeMatchesItemCount_ReturnsNullContinuation()
    {
        if (!_fixture.ShouldRun()) return;

        var store = CreateStore();
        var ns = $"ns_paging_exact_{Guid.NewGuid():N}";
        await SeedRunsAsync(store, ns, count: 4);

        var page = await store.ListByNamespaceAsync(ns, pageSize: 4, continuationToken: null, CancellationToken.None);

        page.Items.Should().HaveCount(4);
        page.ContinuationToken.Should().BeNull();
    }

    private static async Task<IReadOnlyList<DiscoveryRun>> SeedRunsAsync(
        CosmosDiscoveryRunStore store,
        string namespaceId,
        int count)
    {
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-20);
        var seeded = new List<DiscoveryRun>(count);
        for (var i = 0; i < count; i++)
        {
            var run = new DiscoveryRun(
                Id: $"dr_paging_{Guid.NewGuid():N}"[..18],
                SchemaVersion: DiscoveryRun.CurrentSchemaVersion,
                NamespaceId: namespaceId,
                Status: DiscoveryRunStatus.Succeeded,
                Trigger: DiscoveryTrigger.Manual,
                StartedUtc: baseTime.AddSeconds(i),
                CompletedUtc: baseTime.AddSeconds(i + 30),
                DurationMs: 30000,
                RequestedBy: "user-paging",
                QueueCount: 0, TopicCount: 0, SubscriptionCount: 0, RuleCount: 0,
                NewCount: 0, UpdatedCount: 0, UnchangedCount: 0, MissingCount: 0,
                Failure: null,
                CoalescedRequests: Array.Empty<CoalescedRequest>(),
                CorrelationId: "00-paging-test");
            await store.CreateAsync(run, CancellationToken.None);
            seeded.Add(run);
        }
        return seeded;
    }
}
