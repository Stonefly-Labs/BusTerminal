using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T100 / FR-009 / SC-002 evidence.
//
// Covers the two assertions US2 is actually accountable for:
//   1. "all resources owned by Team X" is an indexable query that returns the
//      operational-resource set without N+1 lookups.
//   2. Ownership references are rename-safe: renaming a Team's logical Name
//      (without touching its Id) does not break the references.
//
// The fixture set under 01-base.json owns every operational document under the
// single SampleTeam (Guid 11111111-...-000000000001). The test loads the
// fixtures, runs the OwnedByTeam query, mutates the team's Name, and re-runs the
// query to prove identifier-based linkage rather than name-based inference.
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class OwnershipQueryIntegrationTests
{
    private static readonly ResourceId SampleTeamId =
        ResourceId.Parse("11111111-1111-1111-1111-000000000001");

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "01-base.json");

    private readonly CosmosEmulatorFixture _fixture;

    public OwnershipQueryIntegrationTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OwnedByTeam_query_returns_every_resource_referencing_the_team()
    {
        await TruncateAsync();
        var loaded = await LoadFixturesAsync();

        var expected = loaded
            .Where(r => r.Ownership is not null && r.Ownership.OwningTeamId == SampleTeamId)
            .Select(r => r.Id)
            .ToHashSet();

        expected.Should().NotBeEmpty(
            "the 01-base.json fixture set must include operational resources owned by the sample team");

        var actual = await CollectAsync(_fixture.Store.QueryAsync(
            new ResourceQuery.OwnedByTeam(SampleTeamId),
            default));

        actual.Select(r => r.Id).Should().BeEquivalentTo(expected);
        actual.Should().AllSatisfy(r =>
            r.Ownership.Should().NotBeNull("OwnedByTeam can only return resources carrying an ownership block"));
    }

    [Fact]
    public async Task Renaming_a_team_does_not_break_ownership_references()
    {
        await TruncateAsync();
        var loaded = await LoadFixturesAsync();

        var team = loaded.OfType<Team>().Single(t => t.Id == SampleTeamId);
        var ownedBefore = await CollectAsync(_fixture.Store.QueryAsync(
            new ResourceQuery.OwnedByTeam(SampleTeamId),
            default));
        ownedBefore.Should().NotBeEmpty();

        // Rename the team's logical name (not its Id). The Id is the durable
        // identifier; the Name is human-facing display metadata. References must
        // survive.
        var renamed = team with
        {
            Name = new ResourceName("payments-platform-renamed"),
            DisplayName = "Payments Platform (renamed)",
        };
        var written = await _fixture.Store.UpdateAsync(renamed, TestActor, "integration-test", default);
        written.Name.Value.Should().Be("payments-platform-renamed");
        written.Id.Should().Be(SampleTeamId, "renaming must NEVER change the Id");

        // Re-query — every owned resource must still come back.
        var ownedAfter = await CollectAsync(_fixture.Store.QueryAsync(
            new ResourceQuery.OwnedByTeam(SampleTeamId),
            default));

        ownedAfter.Select(r => r.Id)
            .Should().BeEquivalentTo(ownedBefore.Select(r => r.Id),
                "ownership.owningTeamId is keyed by Id, not Name; renaming must be invisible to the query.");

        // And spot-check: pull one referencing resource by Id and confirm the Id
        // it carries still resolves to the renamed team.
        var firstReferencer = ownedAfter[0];
        var resolvedTeam = await _fixture.Store.GetAsync(
            firstReferencer.Ownership!.OwningTeamId,
            ResourceTypeDiscriminators.Team,
            includeDeleted: false,
            default);
        resolvedTeam.Should().BeOfType<Team>()
            .Which.Name.Value.Should().Be("payments-platform-renamed");
    }

    private async Task<IReadOnlyList<Resource>> LoadFixturesAsync()
    {
        var envelopeJson = await File.ReadAllTextAsync(FixturePath);
        var envelope = _fixture.Serializer.DeserializeEnvelopeFromJson(envelopeJson);

        var written = new List<Resource>(envelope.Resources.Count);
        foreach (var resource in envelope.Resources)
        {
            written.Add(await _fixture.Store.CreateAsync(resource, TestActor, "integration-test", default));
        }

        return written;
    }

    private static async Task<IReadOnlyList<Resource>> CollectAsync(IAsyncEnumerable<Resource> source)
    {
        var list = new List<Resource>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }

    private async Task TruncateAsync()
    {
        await DrainAsync(_fixture.ResourcesCosmosContainer, "resourceType");
        await DrainAsync(_fixture.ChangeEventsCosmosContainer, "resourceId");
    }

    private static async Task DrainAsync(Container container, string partitionKeyField)
    {
        var query = $"SELECT c.id, c[\"{partitionKeyField}\"] AS pk FROM c";
        using var iterator = container.GetItemQueryIterator<DocumentRef>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            foreach (var doc in page)
            {
                if (doc.Pk is null || doc.Id is null)
                {
                    continue;
                }

                await container.DeleteItemAsync<object>(doc.Id, new PartitionKey(doc.Pk));
            }
        }
    }

    private sealed record DocumentRef(string? Id, string? Pk);
}
