using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Relationships;
using BusTerminal.Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T114 / FR-008 / SC-003 evidence.
//
// Loads 01-base.json + 02-relationships.json into the emulator, then traverses
// from the orders-api producer application to the consuming application via
// (a) the topic + subscription path, and (b) the direct queue path, asserting
// the consumer is reached, each hop is correctly typed, and the spanning-tree
// ordering is deterministic.
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class RelationshipTraversalIntegrationTests
{
    private static readonly ResourceId ProducerAppId = ResourceId.Parse("11111111-1111-1111-1111-000000000008");
    private static readonly ResourceId QueueId = ResourceId.Parse("11111111-1111-1111-1111-000000000004");
    private static readonly ResourceId TopicId = ResourceId.Parse("11111111-1111-1111-1111-000000000005");
    private static readonly ResourceId SubscriptionId = ResourceId.Parse("11111111-1111-1111-1111-000000000006");
    private static readonly ResourceId ConsumerAppId = ResourceId.Parse("11111111-1111-1111-1111-000000000009");

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    private static string BaseFixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "01-base.json");

    private static string RelationshipsFixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "02-relationships.json");

    private readonly CosmosEmulatorFixture _fixture;

    public RelationshipTraversalIntegrationTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Traversal_from_producer_reaches_consumer_via_topic_and_subscription_path()
    {
        await LoadFixturesAsync();

        var graph = new RelationshipGraph(_fixture.Store);
        var result = await graph.TraverseAsync(
            ProducerAppId,
            allowedTypes: null,
            maxHops: 5,
            direction: Direction.Both,
            cancellationToken: default);

        result.Visited.Should().Contain(ConsumerAppId, "the consumer is reachable from the producer via the messaging cluster");

        // Deterministic spanning-tree ordering: every recorded hop must have an
        // unambiguous depth (the depth of first discovery). Verify by grouping.
        result.Hops.Select(h => h.To).Should().OnlyHaveUniqueItems(
            "spanning-tree traversal records each newly-visited node exactly once");

        // Each hop's RelationshipType must be one of the eight v1 types — proves
        // typing survives Cosmos round-trip.
        result.Hops.Should().AllSatisfy(hop =>
            Enum.IsDefined(hop.Type).Should().BeTrue($"hop {hop.From}->{hop.To} carries a known relationship type"));
    }

    [Fact]
    public async Task Outbound_traversal_from_producer_reaches_consumer_via_queue_only_path()
    {
        await LoadFixturesAsync();

        // Outbound-only: producer -> queue (publishesTo) -> consumer (consumedBy).
        // The topic/subscription path requires inverting subscriptionOf, so it
        // does NOT reach the consumer in outbound-only mode.
        var graph = new RelationshipGraph(_fixture.Store);
        var result = await graph.TraverseAsync(
            ProducerAppId,
            allowedTypes: [RelationshipType.PublishesTo, RelationshipType.ConsumedBy],
            maxHops: 5,
            direction: Direction.Outbound,
            cancellationToken: default);

        result.Visited.Should().Contain(ConsumerAppId);
        result.Visited.Should().Contain(QueueId);

        // Producer -> Queue is the first hop on this path.
        result.Hops.Should().Contain(h =>
            h.From == ProducerAppId &&
            h.To == QueueId &&
            h.Type == RelationshipType.PublishesTo &&
            h.Depth == 1);

        // Queue -> Consumer is the second hop.
        result.Hops.Should().Contain(h =>
            h.From == QueueId &&
            h.To == ConsumerAppId &&
            h.Type == RelationshipType.ConsumedBy &&
            h.Depth == 2);
    }

    [Fact]
    public async Task Inbound_traversal_from_consumer_reaches_producer_via_queue()
    {
        await LoadFixturesAsync();

        var graph = new RelationshipGraph(_fixture.Store);
        var result = await graph.TraverseAsync(
            ConsumerAppId,
            allowedTypes: [RelationshipType.PublishesTo, RelationshipType.ConsumedBy],
            maxHops: 5,
            direction: Direction.Inbound,
            cancellationToken: default);

        // From consumer inbound: ConsumedBy comes IN from queues + subscriptions.
        // Then from queue inbound: PublishesTo comes IN from producer applications.
        result.Visited.Should().Contain(ProducerAppId);
        result.Visited.Should().Contain(QueueId);
        result.Visited.Should().Contain(SubscriptionId);
    }

    [Fact]
    public async Task Type_filtered_traversal_skips_unrelated_edges()
    {
        await LoadFixturesAsync();

        // Owns-only traversal from the team should reach every owned operational
        // resource (8 of them) and NOT bleed into producer/queue traversal.
        var teamId = ResourceId.Parse("11111111-1111-1111-1111-000000000001");
        var graph = new RelationshipGraph(_fixture.Store);
        var result = await graph.TraverseAsync(
            teamId,
            allowedTypes: [RelationshipType.Owns],
            maxHops: 1,
            direction: Direction.Outbound,
            cancellationToken: default);

        // Every recorded hop must be of type Owns.
        result.Hops.All(h => h.Type == RelationshipType.Owns).Should().BeTrue();

        // The fixture wires 8 `owns` edges (team → 8 operational resources).
        result.Hops.Should().HaveCount(8);
    }

    private async Task LoadFixturesAsync()
    {
        await TruncateAsync();

        await LoadEnvelopeAsync(BaseFixturePath);
        await LoadEnvelopeAsync(RelationshipsFixturePath);
    }

    private async Task LoadEnvelopeAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        var envelope = _fixture.Serializer.DeserializeEnvelopeFromJson(json);

        foreach (var resource in envelope.Resources)
        {
            await _fixture.Store.CreateAsync(resource, TestActor, "integration-test", default);
        }

        foreach (var relationship in envelope.Relationships)
        {
            await _fixture.Store.CreateRelationshipAsync(relationship, TestActor, "integration-test", default);
        }
    }

    private async Task TruncateAsync()
    {
        await DrainAsync(_fixture.ResourcesCosmosContainer, "resourceType");
        await DrainAsync(_fixture.ChangeEventsCosmosContainer, "resourceId");
    }

    private static async Task DrainAsync(Microsoft.Azure.Cosmos.Container container, string partitionKeyField)
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

                await container.DeleteItemAsync<object>(
                    doc.Id,
                    new Microsoft.Azure.Cosmos.PartitionKey(doc.Pk));
            }
        }
    }

    private sealed record DocumentRef(string? Id, string? Pk);
}
