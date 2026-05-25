using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Lifecycle;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T129 / FR-015 / Q5 / SC-012 evidence.
//
// Drives a resource through the full lifecycle a real operator would:
// Create -> Update (non-lifecycle) -> LifecycleTransitioned -> SoftDeleted ->
// Restored, and asserts the change-event log captures every event in order
// with actor, timestamp, source system, concurrency tokens (before/after), and
// a snapshot payload on every event.
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class ChangeEventLogIntegrationTests
{
    private readonly CosmosEmulatorFixture _fixture;

    public ChangeEventLogIntegrationTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    [Fact]
    public async Task Full_event_sequence_is_ordered_and_carries_required_metadata()
    {
        await TruncateAsync();

        var created = await _fixture.Store.CreateAsync(
            BuildTopic("event-log-t"),
            TestActor,
            "integration-test",
            default);

        var renamed = await _fixture.Store.UpdateAsync(
            (Topic)created with { DisplayName = "Renamed for event-log integration test" },
            TestActor,
            "integration-test",
            default);

        var deprecated = await _fixture.Store.UpdateAsync(
            (Topic)renamed with { Lifecycle = LifecycleState.Deprecated },
            TestActor,
            "integration-test",
            default);
        deprecated.Lifecycle.Should().Be(LifecycleState.Deprecated);

        var deleted = await _fixture.Store.SoftDeleteAsync(
            deprecated.Id,
            deprecated.ResourceType,
            deprecated.ConcurrencyToken,
            TestActor,
            "integration-test",
            default);

        var restored = await _fixture.Store.RestoreAsync(
            deleted.Id,
            deleted.ResourceType,
            deleted.ConcurrencyToken,
            TestActor,
            "integration-test",
            default);
        restored.Lifecycle.Should().Be(LifecycleState.Deprecated);

        var events = await CollectAsync(_fixture.ChangeEventLog.QueryAsync(created.Id, default));

        events.Should().HaveCount(5,
            "Created + Updated + LifecycleTransitioned + SoftDeleted + Restored");

        events.Select(e => e.EventType).Should().ContainInOrder(
            ChangeEventType.Created,
            ChangeEventType.Updated,
            ChangeEventType.LifecycleTransitioned,
            ChangeEventType.SoftDeleted,
            ChangeEventType.Restored);

        events.Should().BeInAscendingOrder(e => e.Timestamp,
            "the log is queried with ORDER BY timestamp ASC");

        events.Should().AllSatisfy(e =>
        {
            e.Actor.Should().Be(TestActor, "every event records the writing principal");
            e.SourceSystem.Should().Be("integration-test", "source system propagates from the write call");
            e.ResourceId.Should().Be(created.Id);
            e.ResourceType.Should().Be(created.ResourceType);
            e.ConcurrencyTokenAfter.Value.Should().NotBeEmpty(
                "every event captures the post-write concurrency token");
            e.Snapshot.Should().NotBeNull("every event includes a snapshot per Q5");
        });

        var createdEvent = events.Single(e => e.EventType == ChangeEventType.Created);
        createdEvent.ConcurrencyTokenBefore.Should().BeNull(
            "Created carries no prior token");
        createdEvent.LifecycleBefore.Should().BeNull(
            "Created carries no prior lifecycle");
        createdEvent.LifecycleAfter.Should().Be(LifecycleState.Active);

        var transitionEvent = events.Single(e => e.EventType == ChangeEventType.LifecycleTransitioned);
        transitionEvent.LifecycleBefore.Should().Be(LifecycleState.Active);
        transitionEvent.LifecycleAfter.Should().Be(LifecycleState.Deprecated);
        transitionEvent.ConcurrencyTokenBefore.Should().NotBeNull(
            "non-create writes carry the IfMatch token that gated the write");

        var deletedEvent = events.Single(e => e.EventType == ChangeEventType.SoftDeleted);
        deletedEvent.LifecycleBefore.Should().Be(LifecycleState.Deprecated,
            "soft-delete does not change lifecycle; before == after");
        deletedEvent.LifecycleAfter.Should().Be(LifecycleState.Deprecated);

        var restoredEvent = events.Single(e => e.EventType == ChangeEventType.Restored);
        restoredEvent.LifecycleBefore.Should().Be(LifecycleState.Deprecated);
        restoredEvent.LifecycleAfter.Should().Be(LifecycleState.Deprecated);
    }

    [Fact]
    public async Task Events_for_distinct_resources_are_isolated_by_partition()
    {
        await TruncateAsync();

        var first = await _fixture.Store.CreateAsync(
            BuildTopic("event-partition-1"),
            TestActor,
            "integration-test",
            default);
        var second = await _fixture.Store.CreateAsync(
            BuildTopic("event-partition-2"),
            TestActor,
            "integration-test",
            default);

        var firstEvents = await CollectAsync(_fixture.ChangeEventLog.QueryAsync(first.Id, default));
        var secondEvents = await CollectAsync(_fixture.ChangeEventLog.QueryAsync(second.Id, default));

        firstEvents.Should().ContainSingle();
        secondEvents.Should().ContainSingle();
        firstEvents[0].ResourceId.Should().Be(first.Id);
        secondEvents[0].ResourceId.Should().Be(second.Id);
    }

    private static Topic BuildTopic(string name) => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Topic,
        Name = new ResourceName(name),
        DisplayName = $"Change-event log test topic {name}",
        NamespacePath = new NamespacePath("enterprise/test"),
        Lifecycle = LifecycleState.Active,
        Version = new SemanticVersion(1, 0, 0),
        Audit = new AuditRecord(TestActor, DateTimeOffset.UtcNow, TestActor, DateTimeOffset.UtcNow),
        Ownership = new OwnershipRecord(OwningTeamId: ResourceId.New(), OperationalTier: OperationalTier.Tier1),
        Ordering = OrderingPolicy.Unordered,
    };

    private static async Task<IReadOnlyList<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
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
