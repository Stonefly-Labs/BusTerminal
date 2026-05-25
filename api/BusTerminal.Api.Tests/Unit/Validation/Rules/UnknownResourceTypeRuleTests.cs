using System.Text.Json;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Domain.Validation.Rules;
using BusTerminal.Api.Tests.Unit.Serialization;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Validation.Rules;

// Spec 004 / T090.
public sealed class UnknownResourceTypeRuleTests
{
    private static ValidationContext EmptyContext() => new()
    {
        RelationshipResolver = _ => null,
        DuplicateDetector = _ => false,
        Services = new ServiceProviderStub(),
        PreviousLifecycle = null,
    };

    [Fact]
    public void Rule_only_applies_to_unknown_resource()
    {
        var rule = new UnknownResourceTypeRule(TimeProvider.System);
        rule.AppliesTo(typeof(UnknownResource)).Should().BeTrue();
        rule.AppliesTo(typeof(BusTerminal.Api.Domain.Resources.Queue)).Should().BeFalse();
    }

    [Fact]
    public void Unknown_resource_emits_info_finding()
    {
        var unknown = new UnknownResource
        {
            Id = ResourceId.New(),
            ResourceType = "syntheticFutureType",
            Name = new ResourceName("test"),
            DisplayName = "Test",
            NamespacePath = new NamespacePath("enterprise/test"),
            Lifecycle = LifecycleState.Active,
            Version = new SemanticVersion(1, 0, 0),
            Audit = ResourceFactory.SampleAudit(),
            RawJson = JsonDocument.Parse("{\"resourceType\":\"syntheticFutureType\"}").RootElement,
        };

        var rule = new UnknownResourceTypeRule(TimeProvider.System);
        var findings = rule.Validate(unknown, EmptyContext()).ToList();

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Info);
        findings[0].RuleId.Should().Be(UnknownResourceTypeRule.RuleId);
    }

    private sealed class ServiceProviderStub : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
