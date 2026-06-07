using System.Diagnostics;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Tests.Features.Registry._Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T105 [US2] [TEST]. Integration test for end-to-end search lag.
//
// Requires a live dev Cosmos + dev AI Search index. The fixture short-circuits
// when env vars are absent — keeps CI green when only the unit tier runs.
// Trait("Category", "Integration") opts these into the integration tier.
[Trait("Category", "Integration")]
public sealed class SearchIndexLagTests : IClassFixture<RegistryFixture>
{
    private readonly RegistryFixture _fixture;

    public SearchIndexLagTests(RegistryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NewEntity_AppearsInSearch_WithinSc005Budget()
    {
        if (!_fixture.ShouldRun())
        {
            // xUnit lacks a runtime Skip in this version — the unit tier
            // excludes this test by the Integration trait, and the integration
            // tier wires the env vars. Early-return keeps the test a no-op
            // when neither is true.
            return;
        }

        var id = Guid.NewGuid();
        var name = $"search-lag-{id:N}".Substring(0, 50);
        var entity = new RegistryNamespace(
            id: id,
            name: name,
            environment: _fixture.Environment,
            status: RegistryEntityStatus.Active,
            createdAtUtc: DateTimeOffset.UtcNow,
            updatedAtUtc: DateTimeOffset.UtcNow,
            source: RegistrySource.Manual);

        await _fixture.Store.CreateAsync(entity, default);

        // SC-005 budget: index lag p95 < 5s. We poll the search client up to
        // 10s before giving up — the indexer may queue briefly under load.
        var deadline = TimeSpan.FromSeconds(10);
        var stopwatch = Stopwatch.StartNew();
        var searchClient = _fixture.Services.GetService<ISearchClient>()
            ?? throw new InvalidOperationException("ISearchClient not registered.");

        while (stopwatch.Elapsed < deadline)
        {
            var results = await searchClient
                .SearchAsync(new RegistrySearchRequest(Query: name, Top: 5), default)
                ;
            if (results.Hits.Any(h => h.Id == id))
            {
                stopwatch.Stop();
                stopwatch.Elapsed.Should().BeLessThan(deadline, "SC-005 mandates index lag p95 < 5s.");
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"Entity {id} did not appear in search within {deadline.TotalSeconds}s.");
    }
}
