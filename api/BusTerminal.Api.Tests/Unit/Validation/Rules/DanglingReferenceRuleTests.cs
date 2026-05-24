using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Domain.Validation.Rules;
using BusTerminal.Api.Tests.Unit.Serialization;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Validation.Rules;

// Spec 004 / T112 / FR-008. Locks in the live-target pass / missing-target Error
// / soft-deleted-target Warning matrix across the typed reference fields the
// rule covers.
public sealed class DanglingReferenceRuleTests
{
    private static readonly ResourceId MissingId = ResourceId.Parse("ffffffff-0000-0000-0000-000000000001");

    [Fact]
    public void Subscription_with_resolvable_parent_topic_passes()
    {
        var topic = ResourceFactory.BuildTopic();
        var subscription = ResourceFactory.BuildSubscription() with { ParentTopicId = topic.Id };

        var findings = Run(subscription, [topic]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Subscription_with_missing_parent_topic_fires_error()
    {
        var subscription = ResourceFactory.BuildSubscription() with { ParentTopicId = MissingId };

        var findings = Run(subscription, resources: []);

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Error);
        findings[0].FieldRef.Should().Be("/parentTopicId");
        findings[0].RelationshipRef.Should().Be(MissingId);
        findings[0].RuleId.Should().Be(DanglingReferenceRule.RuleId);
    }

    [Fact]
    public void Subscription_with_soft_deleted_parent_topic_fires_warning()
    {
        var deletedTopic = ResourceFactory.BuildTopic() with { IsDeleted = true };
        var subscription = ResourceFactory.BuildSubscription() with { ParentTopicId = deletedTopic.Id };

        var findings = Run(subscription, [deletedTopic]);

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Warning);
        findings[0].FieldRef.Should().Be("/parentTopicId");
        findings[0].Message.Should().Contain("soft-deleted");
    }

    [Fact]
    public void IntegrationFlow_with_missing_producer_fires_error()
    {
        var flow = ResourceFactory.BuildIntegrationFlow();

        // The fixture flow references arbitrary Guids; no referents exist.
        var findings = Run(flow, resources: []);

        findings.Should().NotBeEmpty();
        findings.Should().Contain(f =>
            f.Severity == ValidationSeverity.Error && f.FieldRef == "/producerApplicationId");
        findings.Should().Contain(f =>
            f.Severity == ValidationSeverity.Error && f.FieldRef == "/messagingResourceId");
    }

    [Fact]
    public void DocumentationAsset_with_resolvable_attached_resources_passes()
    {
        var queue = ResourceFactory.BuildQueue();
        var doc = ResourceFactory.BuildDocumentationAsset() with
        {
            AttachedResourceIds = [queue.Id],
        };

        var findings = Run(doc, [queue]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void DocumentationAsset_with_missing_attached_resource_fires_error()
    {
        var doc = ResourceFactory.BuildDocumentationAsset() with
        {
            AttachedResourceIds = [MissingId],
        };

        var findings = Run(doc, resources: []);

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Error);
        findings[0].FieldRef.Should().Be("/attachedResourceIds");
    }

    [Fact]
    public void Ownership_owningTeamId_is_NOT_re_checked_by_dangling_rule()
    {
        // OwnershipPresenceRule handles this with stricter semantics; this rule
        // explicitly skips it to avoid duplicate findings.
        var queue = ResourceFactory.BuildQueue() with
        {
            Ownership = new OwnershipRecord(MissingId, OperationalTier.Tier1),
        };

        var findings = Run(queue, resources: []);

        // No finding about /ownership/owningTeamId should be present.
        findings.Should().NotContain(f => f.FieldRef == "/ownership/owningTeamId");
    }

    [Fact]
    public void Queue_with_resolvable_contract_and_producer_passes()
    {
        var contract = ResourceFactory.BuildMessageContract();
        var producer = ResourceFactory.BuildProducerApplication();
        var queue = ResourceFactory.BuildQueue() with
        {
            ContractAssociations = [new ContractReference(contract.Id)],
            Producers = [new ApplicationReference(producer.Id)],
        };

        var findings = Run(queue, [contract, producer]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Tag_reference_pointing_at_missing_TagResource_fires_error()
    {
        var queue = ResourceFactory.BuildQueue() with
        {
            Tags = [new TagReference(MissingId, "missing-tag")],
        };

        var findings = Run(queue, resources: []);

        findings.Should().Contain(f => f.FieldRef == "/tags" && f.Severity == ValidationSeverity.Error);
    }

    private static IReadOnlyList<ValidationFinding> Run(Resource subject, IReadOnlyList<Resource> resources)
    {
        var index = resources.ToDictionary(r => r.Id, r => r);
        var rule = new DanglingReferenceRule(TimeProvider.System);
        var ctx = new ValidationContext
        {
            RelationshipResolver = id => index.TryGetValue(id, out var r) ? r : null,
            DuplicateDetector = _ => false,
            Services = new EmptyServiceProvider(),
        };

        return rule.Validate(subject, ctx).ToList();
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
