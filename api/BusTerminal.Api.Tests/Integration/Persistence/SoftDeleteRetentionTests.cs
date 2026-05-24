using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Lifecycle;
using BusTerminal.Api.Domain.Relationships;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T128 / FR-020 / SC-005 evidence.
//
// Soft-delete preserves identifier, audit, version lineage, ownership,
// lifecycle, and relationships. Restoration returns the resource to its prior
// lifecycle state. Both operations emit dedicated SoftDeleted/Restored events
// into the change-event log.
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class SoftDeleteRetentionTests
{
    private readonly CosmosEmulatorFixture _fixture;

    public SoftDeleteRetentionTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    [Fact]
    public async Task SoftDelete_preserves_identifier_audit_ownership_lifecycle_and_version()
    {
        await TruncateAsync();
        var topic = await _fixture.Store.CreateAsync(
            BuildTopic("soft-delete-preserve-t"),
            TestActor,
            "integration-test",
            default);

        var originalAudit = topic.Audit;
        var originalLifecycle = topic.Lifecycle;
        var originalVersion = topic.Version;
        var originalOwnership = topic.Ownership;

        var deleted = await _fixture.Store.SoftDeleteAsync(
            topic.Id,
            topic.ResourceType,
            topic.ConcurrencyToken,
            TestActor,
            "integration-test",
            default);

        deleted.IsDeleted.Should().BeTrue();
        deleted.Id.Should().Be(topic.Id, "identifier survives soft-delete");
        deleted.Lifecycle.Should().Be(originalLifecycle, "soft-delete is orthogonal to lifecycle");
        deleted.Version.Should().BeEquivalentTo(originalVersion, "version lineage is preserved");
        deleted.Ownership.Should().BeEquivalentTo(originalOwnership, "ownership references survive soft-delete");
        deleted.Audit.CreatedAt.Should().Be(originalAudit.CreatedAt, "creation timestamp preserved");
        deleted.Audit.CreatedBy.Should().Be(originalAudit.CreatedBy, "creator preserved");

        var roundTrip = await _fixture.Store.GetAsync(topic.Id, topic.ResourceType, includeDeleted: true, default);
        roundTrip.Should().NotBeNull();
        roundTrip!.IsDeleted.Should().BeTrue();
        roundTrip.Id.Should().Be(topic.Id);
    }

    [Fact]
    public async Task SoftDelete_hides_resource_from_default_reads_but_includeDeleted_recovers_it()
    {
        await TruncateAsync();
        var queue = await _fixture.Store.CreateAsync(
            BuildQueue("soft-delete-visibility-q"),
            TestActor,
            "integration-test",
            default);

        await _fixture.Store.SoftDeleteAsync(
            queue.Id,
            queue.ResourceType,
            queue.ConcurrencyToken,
            TestActor,
            "integration-test",
            default);

        var hidden = await _fixture.Store.GetAsync(
            queue.Id, queue.ResourceType, includeDeleted: false, default);
        hidden.Should().BeNull("default reads filter out soft-deleted documents");

        var visible = await _fixture.Store.GetAsync(
            queue.Id, queue.ResourceType, includeDeleted: true, default);
        visible.Should().NotBeNull();
        visible!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Restore_returns_resource_to_prior_lifecycle_state()
    {
        await TruncateAsync();

        // Move topic into Deprecated before soft-delete so the test exercises a
        // non-default Lifecycle preservation path.
        var topic = await _fixture.Store.CreateAsync(
            BuildTopic("soft-delete-restore-t"),
            TestActor,
            "integration-test",
            default);

        var deprecated = await _fixture.Store.UpdateAsync(
            (Topic)topic with { Lifecycle = LifecycleState.Deprecated },
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

        restored.IsDeleted.Should().BeFalse();
        restored.Lifecycle.Should().Be(LifecycleState.Deprecated,
            "restore returns the resource to the Lifecycle it carried at soft-delete time");
    }

    [Fact]
    public async Task Relationships_referencing_the_resource_survive_soft_delete()
    {
        await TruncateAsync();
        var producer = await _fixture.Store.CreateAsync(
            BuildProducerApplication("rel-preserve-producer"),
            TestActor,
            "integration-test",
            default);
        var queue = await _fixture.Store.CreateAsync(
            BuildQueue("rel-preserve-q"),
            TestActor,
            "integration-test",
            default);

        var relationship = await _fixture.Store.CreateRelationshipAsync(
            new Relationship
            {
                Id = ResourceId.New(),
                Audit = new AuditRecord(TestActor, DateTimeOffset.UtcNow, TestActor, DateTimeOffset.UtcNow),
                SourceId = producer.Id,
                TargetId = queue.Id,
                Type = RelationshipType.PublishesTo,
            },
            TestActor,
            "integration-test",
            default);

        await _fixture.Store.SoftDeleteAsync(
            queue.Id,
            queue.ResourceType,
            queue.ConcurrencyToken,
            TestActor,
            "integration-test",
            default);

        var byEndpoint = await CollectAsync(_fixture.Store.QueryRelationshipsAsync(
            new RelationshipQuery.ByEndpoint(queue.Id, Direction.Inbound),
            default));

        byEndpoint.Should().ContainSingle(r => r.Id == relationship.Id,
            "soft-deleting the queue must not erase relationships referencing it");
    }

    [Fact]
    public async Task SoftDelete_emits_SoftDeleted_change_event()
    {
        await TruncateAsync();
        var queue = await _fixture.Store.CreateAsync(
            BuildQueue("soft-delete-event-q"),
            TestActor,
            "integration-test",
            default);

        await _fixture.Store.SoftDeleteAsync(
            queue.Id,
            queue.ResourceType,
            queue.ConcurrencyToken,
            TestActor,
            "integration-test",
            default);

        var events = await CollectAsync(_fixture.ChangeEventLog.QueryAsync(queue.Id, default));
        events.Should().Contain(e => e.EventType == ChangeEventType.SoftDeleted);
    }

    [Fact]
    public async Task Restore_emits_Restored_change_event()
    {
        await TruncateAsync();
        var queue = await _fixture.Store.CreateAsync(
            BuildQueue("restore-event-q"),
            TestActor,
            "integration-test",
            default);

        var deleted = await _fixture.Store.SoftDeleteAsync(
            queue.Id,
            queue.ResourceType,
            queue.ConcurrencyToken,
            TestActor,
            "integration-test",
            default);

        await _fixture.Store.RestoreAsync(
            deleted.Id,
            deleted.ResourceType,
            deleted.ConcurrencyToken,
            TestActor,
            "integration-test",
            default);

        var events = await CollectAsync(_fixture.ChangeEventLog.QueryAsync(queue.Id, default));
        events.Should().Contain(e => e.EventType == ChangeEventType.Restored);
    }

    private static Queue BuildQueue(string name) => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Queue,
        Name = new ResourceName(name),
        DisplayName = $"Soft-delete test queue {name}",
        NamespacePath = new NamespacePath("enterprise/test"),
        Lifecycle = LifecycleState.Active,
        Version = new SemanticVersion(1, 0, 0),
        Audit = new AuditRecord(TestActor, DateTimeOffset.UtcNow, TestActor, DateTimeOffset.UtcNow),
        Ownership = new OwnershipRecord(OwningTeamId: ResourceId.New(), OperationalTier: OperationalTier.Tier2),
        QueueKind = "AzureServiceBus",
        Ordering = OrderingPolicy.Fifo,
    };

    private static Topic BuildTopic(string name) => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Topic,
        Name = new ResourceName(name),
        DisplayName = $"Soft-delete test topic {name}",
        NamespacePath = new NamespacePath("enterprise/test"),
        Lifecycle = LifecycleState.Active,
        Version = new SemanticVersion(1, 0, 0),
        Audit = new AuditRecord(TestActor, DateTimeOffset.UtcNow, TestActor, DateTimeOffset.UtcNow),
        Ownership = new OwnershipRecord(OwningTeamId: ResourceId.New(), OperationalTier: OperationalTier.Tier1),
        Ordering = OrderingPolicy.Unordered,
    };

    private static ProducerApplication BuildProducerApplication(string name) => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.ProducerApplication,
        Name = new ResourceName(name),
        DisplayName = $"Producer {name}",
        NamespacePath = new NamespacePath("enterprise/test"),
        Lifecycle = LifecycleState.Active,
        Version = new SemanticVersion(1, 0, 0),
        Audit = new AuditRecord(TestActor, DateTimeOffset.UtcNow, TestActor, DateTimeOffset.UtcNow),
        Ownership = new OwnershipRecord(OwningTeamId: ResourceId.New(), OperationalTier: OperationalTier.Tier2),
        ApplicationKind = "WebService",
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
