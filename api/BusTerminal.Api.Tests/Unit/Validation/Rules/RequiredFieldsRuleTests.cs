using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Domain.Validation.Rules;
using BusTerminal.Api.Tests.Unit.Serialization;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Validation.Rules;

// Spec 004 / T090.
public sealed class RequiredFieldsRuleTests
{
    private static ValidationContext EmptyContext() => new()
    {
        RelationshipResolver = _ => null,
        DuplicateDetector = _ => false,
        Services = new ServiceProviderStub(),
        PreviousLifecycle = null,
    };

    [Fact]
    public void Complete_resource_passes()
    {
        var rule = new RequiredFieldsRule(TimeProvider.System);
        var findings = rule.Validate(ResourceFactory.BuildQueue(), EmptyContext()).ToList();
        findings.Should().BeEmpty();
    }

    [Fact]
    public void Missing_display_name_fires_error()
    {
        var rule = new RequiredFieldsRule(TimeProvider.System);
        var resource = ResourceFactory.BuildQueue() with { DisplayName = "" };
        var findings = rule.Validate(resource, EmptyContext()).ToList();

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Error);
        findings[0].RuleId.Should().Be(RequiredFieldsRule.RuleId);
        findings[0].FieldRef.Should().Be("/displayName");
    }

    [Fact]
    public void Missing_id_fires_error()
    {
        var rule = new RequiredFieldsRule(TimeProvider.System);
        var resource = ResourceFactory.BuildQueue() with { Id = default };
        var findings = rule.Validate(resource, EmptyContext()).ToList();

        findings.Should().Contain(f => f.FieldRef == "/id" && f.Severity == ValidationSeverity.Error);
    }

    private sealed class ServiceProviderStub : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
