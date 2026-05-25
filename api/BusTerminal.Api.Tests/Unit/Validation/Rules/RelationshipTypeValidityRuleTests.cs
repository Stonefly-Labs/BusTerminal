using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Relationships;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Domain.Validation.Rules;
using BusTerminal.Api.Tests.Unit.Serialization;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Validation.Rules;

// Spec 004 / T113 / FR-008. Locks in the source/target pairing matrix from
// contracts/relationship-types.md plus the no-self-relationship and the
// matching-endpoint-type rules.
public sealed class RelationshipTypeValidityRuleTests
{
    [Fact]
    public void PublishesTo_producer_to_topic_passes()
    {
        var producer = ResourceFactory.BuildProducerApplication();
        var topic = ResourceFactory.BuildTopic();
        var rel = Rel(producer.Id, topic.Id, RelationshipType.PublishesTo);

        var findings = Run(rel, [producer, topic]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void PublishesTo_producer_to_queue_passes()
    {
        var producer = ResourceFactory.BuildProducerApplication();
        var queue = ResourceFactory.BuildQueue();
        var rel = Rel(producer.Id, queue.Id, RelationshipType.PublishesTo);

        var findings = Run(rel, [producer, queue]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void PublishesTo_consumer_to_topic_fires_error_for_bad_source()
    {
        var consumer = ResourceFactory.BuildConsumerApplication();
        var topic = ResourceFactory.BuildTopic();
        var rel = Rel(consumer.Id, topic.Id, RelationshipType.PublishesTo);

        var findings = Run(rel, [consumer, topic]);

        findings.Should().Contain(f =>
            f.FieldRef == "/sourceId" && f.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ConsumedBy_queue_to_consumer_passes()
    {
        var queue = ResourceFactory.BuildQueue();
        var consumer = ResourceFactory.BuildConsumerApplication();
        var rel = Rel(queue.Id, consumer.Id, RelationshipType.ConsumedBy);

        var findings = Run(rel, [queue, consumer]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void ConsumedBy_subscription_to_consumer_passes()
    {
        var subscription = ResourceFactory.BuildSubscription();
        var consumer = ResourceFactory.BuildConsumerApplication();
        var rel = Rel(subscription.Id, consumer.Id, RelationshipType.ConsumedBy);

        var findings = Run(rel, [subscription, consumer]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void SubscriptionOf_subscription_to_topic_passes()
    {
        var subscription = ResourceFactory.BuildSubscription();
        var topic = ResourceFactory.BuildTopic();
        var rel = Rel(subscription.Id, topic.Id, RelationshipType.SubscriptionOf);

        var findings = Run(rel, [subscription, topic]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void UsesContract_queue_to_contract_passes()
    {
        var queue = ResourceFactory.BuildQueue();
        var contract = ResourceFactory.BuildMessageContract();
        var rel = Rel(queue.Id, contract.Id, RelationshipType.UsesContract);

        var findings = Run(rel, [queue, contract]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Owns_team_to_operational_resource_passes()
    {
        var team = ResourceFactory.BuildTeam();
        var queue = ResourceFactory.BuildQueue();
        var rel = Rel(team.Id, queue.Id, RelationshipType.Owns);

        var findings = Run(rel, [team, queue]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Owns_team_to_non_operational_resource_fires_error_for_bad_target()
    {
        // Namespace is not an operational type.
        var team = ResourceFactory.BuildTeam();
        var ns = ResourceFactory.BuildNamespace();
        var rel = Rel(team.Id, ns.Id, RelationshipType.Owns);

        var findings = Run(rel, [team, ns]);

        findings.Should().Contain(f =>
            f.FieldRef == "/targetId" && f.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void AttachedTo_documentation_to_any_passes()
    {
        var doc = ResourceFactory.BuildDocumentationAsset();
        var queue = ResourceFactory.BuildQueue();
        var rel = Rel(doc.Id, queue.Id, RelationshipType.AttachedTo);

        var findings = Run(rel, [doc, queue]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Replaces_with_matching_endpoint_types_passes()
    {
        var oldQueue = ResourceFactory.BuildQueue();
        var newQueue = ResourceFactory.BuildQueue();
        var rel = Rel(newQueue.Id, oldQueue.Id, RelationshipType.Replaces);

        var findings = Run(rel, [oldQueue, newQueue]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Replaces_with_mismatched_endpoint_types_fires_error()
    {
        var queue = ResourceFactory.BuildQueue();
        var topic = ResourceFactory.BuildTopic();
        var rel = Rel(queue.Id, topic.Id, RelationshipType.Replaces);

        var findings = Run(rel, [queue, topic]);

        findings.Should().Contain(f =>
            f.FieldRef == "/type" && f.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void PartOfFlow_with_disallowed_source_fires_error()
    {
        // Team is not a permitted source for partOfFlow.
        var team = ResourceFactory.BuildTeam();
        var flow = ResourceFactory.BuildIntegrationFlow();
        var rel = Rel(team.Id, flow.Id, RelationshipType.PartOfFlow);

        var findings = Run(rel, [team, flow]);

        findings.Should().Contain(f =>
            f.FieldRef == "/sourceId" && f.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Self_relationship_fires_error()
    {
        var topic = ResourceFactory.BuildTopic();
        var producer = ResourceFactory.BuildProducerApplication();
        // Use the same id on both endpoints — the producer publishes to itself.
        var rel = Rel(producer.Id, producer.Id, RelationshipType.PublishesTo);

        var findings = Run(rel, [producer, topic]);

        findings.Should().Contain(f =>
            f.Severity == ValidationSeverity.Error &&
            f.Message.Contains("self-referential", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Missing_source_or_target_resource_fires_error()
    {
        // Neither endpoint resolves.
        var rel = Rel(ResourceId.New(), ResourceId.New(), RelationshipType.PublishesTo);

        var findings = Run(rel, resources: []);

        findings.Should().Contain(f => f.FieldRef == "/sourceId" && f.Severity == ValidationSeverity.Error);
        findings.Should().Contain(f => f.FieldRef == "/targetId" && f.Severity == ValidationSeverity.Error);
    }

    private static Relationship Rel(ResourceId source, ResourceId target, RelationshipType type) => new()
    {
        Id = ResourceId.New(),
        SourceId = source,
        TargetId = target,
        Type = type,
        Audit = new AuditRecord(
            CreatedBy: new SystemPrincipalReference("test"),
            CreatedAt: DateTimeOffset.UnixEpoch,
            ModifiedBy: new SystemPrincipalReference("test"),
            ModifiedAt: DateTimeOffset.UnixEpoch),
    };

    private static IReadOnlyList<ValidationFinding> Run(Relationship rel, IReadOnlyList<Resource> resources)
    {
        var index = resources.ToDictionary(r => r.Id, r => r);
        var rule = new RelationshipTypeValidityRule(TimeProvider.System);
        var ctx = new ValidationContext
        {
            RelationshipResolver = id => index.TryGetValue(id, out var r) ? r : null,
            DuplicateDetector = _ => false,
            Services = new EmptyServiceProvider(),
        };

        return rule.Validate(rel, ctx).ToList();
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
