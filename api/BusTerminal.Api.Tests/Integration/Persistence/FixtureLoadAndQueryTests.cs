using System.Text.Json;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Lifecycle;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Infrastructure.Persistence;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T092 / SC-001 final evidence. Loads the 01-base.json fixture set
// into the emulator, asserts every first-class type round-trips through Cosmos
// with identifiers stable, lifecycle preserved, namespace paths intact.
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class FixtureLoadAndQueryTests
{
    private readonly CosmosEmulatorFixture _fixture;

    public FixtureLoadAndQueryTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "01-base.json");

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    [Fact]
    public async Task Loaded_fixture_set_round_trips_through_cosmos()
    {
        await TruncateAsync();

        var envelopeJson = await File.ReadAllTextAsync(FixturePath);
        var envelope = _fixture.Serializer.DeserializeEnvelopeFromJson(envelopeJson);

        envelope.Resources.Should().HaveCount(14, "01-base.json must include one of each first-class resource type");

        var byId = new Dictionary<ResourceId, Resource>();
        foreach (var resource in envelope.Resources)
        {
            var written = await _fixture.Store.CreateAsync(resource, TestActor, "integration-test", default);
            byId[written.Id] = written;
            written.ConcurrencyToken.Value.Should().NotBeEmpty("Cosmos stamps an _etag on create");
        }

        foreach (var (id, original) in byId)
        {
            var read = await _fixture.Store.GetAsync(id, original.ResourceType, includeDeleted: false, default);
            read.Should().NotBeNull($"resource {id} ({original.ResourceType}) must be readable after create");
            read!.GetType().Should().Be(original.GetType(), "polymorphic round-trip preserves the concrete type");
            read.Id.Should().Be(original.Id);
            read.Name.Value.Should().Be(original.Name.Value);
            read.NamespacePath.Value.Should().Be(original.NamespacePath.Value);
            read.Lifecycle.Should().Be(original.Lifecycle);
        }
    }

    [Fact]
    public async Task Unknown_resourceType_round_trips_as_UnknownResource_with_raw_payload()
    {
        await TruncateAsync();

        // Build a synthetic document by serializing a known queue + mutating its
        // discriminator. Persist directly via the underlying container (we can't
        // CreateAsync because the store would dispatch through the registry —
        // but with discriminator-changed-on-the-wire the registry refuses and
        // the converter falls through to UnknownResource on read).
        var queue = SampleQueue();
        var json = _fixture.Serializer.SerializeToJson(queue);
        var mutatedJson = json.Replace("\"queue\"", "\"syntheticFutureType\"", StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(mutatedJson);

        var container = _fixture.ResourcesCosmosContainer;
        await container.CreateItemAsync(
            doc.RootElement,
            new Microsoft.Azure.Cosmos.PartitionKey("syntheticFutureType"));

        var read = await _fixture.Store.GetAsync(queue.Id, "syntheticFutureType", includeDeleted: false, default);

        read.Should().BeOfType<UnknownResource>();
        read!.ResourceType.Should().Be("syntheticFutureType");
        ((UnknownResource)read).RawJson.GetProperty("name").GetString().Should().Be(queue.Name.Value);
    }

    private static Queue SampleQueue() => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Queue,
        Name = new ResourceName("integration-test-q"),
        DisplayName = "Integration test queue",
        NamespacePath = new NamespacePath("enterprise/test"),
        Lifecycle = LifecycleState.Active,
        Version = new SemanticVersion(1, 0, 0),
        Audit = new AuditRecord(
            CreatedBy: TestActor,
            CreatedAt: DateTimeOffset.UtcNow,
            ModifiedBy: TestActor,
            ModifiedAt: DateTimeOffset.UtcNow),
        Ownership = new OwnershipRecord(
            OwningTeamId: ResourceId.New(),
            OperationalTier: OperationalTier.Tier1),
        QueueKind = "AzureServiceBus",
        Ordering = OrderingPolicy.Fifo,
    };

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
