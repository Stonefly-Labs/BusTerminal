using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Domain.Validation.Rules;
using BusTerminal.Api.Tests.Unit.Serialization;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Validation.Rules;

// Spec 004 / T099 / FR-009. Locks in the operational-vs-non-operational split
// and the dangling-team-reference behavior. The 8 operational types fail when
// ownership is null; the 6 non-operational types are skipped entirely (the rule
// declares AppliesTo so they never see the engine).
public sealed class OwnershipPresenceRuleTests
{
    public static TheoryData<Type> OperationalTypes() =>
    [
        typeof(Broker),
        typeof(Queue),
        typeof(Topic),
        typeof(Subscription),
        typeof(MessageContract),
        typeof(ProducerApplication),
        typeof(ConsumerApplication),
        typeof(IntegrationFlow),
    ];

    public static TheoryData<Type> NonOperationalTypes() =>
    [
        typeof(Namespace),
        typeof(Team),
        typeof(EnvironmentResource),
        typeof(TagResource),
        typeof(Policy),
        typeof(DocumentationAsset),
    ];

    public static TheoryData<Resource> OperationalResources() =>
    [
        ResourceFactory.BuildBroker(),
        ResourceFactory.BuildQueue(),
        ResourceFactory.BuildTopic(),
        ResourceFactory.BuildSubscription(),
        ResourceFactory.BuildMessageContract(),
        ResourceFactory.BuildProducerApplication(),
        ResourceFactory.BuildConsumerApplication(),
        ResourceFactory.BuildIntegrationFlow(),
    ];

    [Theory]
    [MemberData(nameof(OperationalTypes))]
    public void AppliesTo_returns_true_for_operational_types(Type resourceType)
    {
        var rule = new OwnershipPresenceRule(TimeProvider.System);
        rule.AppliesTo(resourceType).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NonOperationalTypes))]
    public void AppliesTo_returns_false_for_non_operational_types(Type resourceType)
    {
        var rule = new OwnershipPresenceRule(TimeProvider.System);
        rule.AppliesTo(resourceType).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(OperationalResources))]
    public void Operational_resource_without_ownership_fires_error(Resource resource)
    {
        var rule = new OwnershipPresenceRule(TimeProvider.System);
        var withoutOwner = StripOwnership(resource);

        var findings = rule.Validate(withoutOwner, BuildContext(team: null)).ToList();

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Error);
        findings[0].RuleId.Should().Be(OwnershipPresenceRule.RuleId);
        findings[0].FieldRef.Should().Be("/ownership");
    }

    [Fact]
    public void Operational_resource_with_resolvable_team_passes()
    {
        var team = ResourceFactory.BuildTeam();
        var queue = ResourceFactory.BuildQueue() with
        {
            Ownership = new OwnershipRecord(team.Id, OperationalTier.Tier1),
        };

        var rule = new OwnershipPresenceRule(TimeProvider.System);
        var findings = rule.Validate(queue, BuildContext(team)).ToList();

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Dangling_team_reference_fires_error()
    {
        var queue = ResourceFactory.BuildQueue() with
        {
            Ownership = new OwnershipRecord(ResourceId.New(), OperationalTier.Tier1),
        };

        var rule = new OwnershipPresenceRule(TimeProvider.System);
        var findings = rule.Validate(queue, BuildContext(team: null)).ToList();

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Error);
        findings[0].FieldRef.Should().Be("/ownership/owningTeamId");
        findings[0].RelationshipRef.Should().Be(queue.Ownership!.OwningTeamId);
    }

    [Fact]
    public void Team_reference_pointing_at_non_team_fires_error()
    {
        // Operator typo: ownership.owningTeamId resolves to a Namespace, not a Team.
        var namespaceResource = ResourceFactory.BuildNamespace();
        var queue = ResourceFactory.BuildQueue() with
        {
            Ownership = new OwnershipRecord(namespaceResource.Id, OperationalTier.Tier1),
        };

        var rule = new OwnershipPresenceRule(TimeProvider.System);
        var resolver = BuildResolver([namespaceResource]);
        var findings = rule.Validate(queue, BuildContextWithResolver(resolver)).ToList();

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Error);
        findings[0].Message.Should().Contain("not a Team");
    }

    [Fact]
    public void Soft_deleted_team_reference_fires_error()
    {
        var team = ResourceFactory.BuildTeam() with { IsDeleted = true };
        var queue = ResourceFactory.BuildQueue() with
        {
            Ownership = new OwnershipRecord(team.Id, OperationalTier.Tier1),
        };

        var rule = new OwnershipPresenceRule(TimeProvider.System);
        var findings = rule.Validate(queue, BuildContext(team)).ToList();

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Error);
        findings[0].Message.Should().Contain("soft-deleted");
    }

    private static Resource StripOwnership(Resource resource) => resource switch
    {
        Broker b => b with { Ownership = null },
        Queue q => q with { Ownership = null },
        Topic t => t with { Ownership = null },
        Subscription s => s with { Ownership = null },
        MessageContract m => m with { Ownership = null },
        ProducerApplication p => p with { Ownership = null },
        ConsumerApplication c => c with { Ownership = null },
        IntegrationFlow i => i with { Ownership = null },
        _ => throw new ArgumentException($"Unhandled operational type {resource.GetType().Name}", nameof(resource)),
    };

    private static ValidationContext BuildContext(Team? team)
    {
        var resolver = team is null
            ? (Func<ResourceId, Resource?>)(_ => null)
            : id => id == team.Id ? team : null;

        return BuildContextWithResolver(resolver);
    }

    private static ValidationContext BuildContextWithResolver(Func<ResourceId, Resource?> resolver) => new()
    {
        RelationshipResolver = resolver,
        DuplicateDetector = _ => false,
        Services = new EmptyServiceProvider(),
        PreviousLifecycle = null,
    };

    private static Func<ResourceId, Resource?> BuildResolver(IEnumerable<Resource> resources)
    {
        var index = resources.ToDictionary(r => r.Id, r => r);
        return id => index.TryGetValue(id, out var r) ? r : null;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
