using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Domain.Validation.Rules;
using BusTerminal.Api.Tests.Unit.Serialization;
using FluentAssertions;
using System.Reflection;

namespace BusTerminal.Api.Tests.Unit.Validation.Rules;

// Spec 004 / T090.
public sealed class NamingStandardsRuleTests
{
    private static ValidationContext EmptyContext() => new()
    {
        RelationshipResolver = _ => null,
        DuplicateDetector = _ => false,
        Services = new ServiceProviderStub(),
        PreviousLifecycle = null,
    };

    [Fact]
    public void Compliant_name_passes()
    {
        var rule = new NamingStandardsRule(TimeProvider.System);
        var findings = rule.Validate(ResourceFactory.BuildQueue(), EmptyContext()).ToList();
        findings.Should().BeEmpty();
    }

    [Fact]
    public void Bypass_constructed_name_with_invalid_pattern_fires_error()
    {
        // ResourceName's ctor blocks invalid input, so simulate the
        // future-ingest-path scenario by using reflection to mutate the
        // backing field on a constructed instance.
        var resource = ResourceFactory.BuildQueue();
        var mutated = ForceName(resource, "INVALID_NAME");

        var rule = new NamingStandardsRule(TimeProvider.System);
        var findings = rule.Validate(mutated, EmptyContext()).ToList();

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Error);
        findings[0].RuleId.Should().Be(NamingStandardsRule.RuleId);
        findings[0].FieldRef.Should().Be("/name");
    }

    private static Resource ForceName(Resource resource, string rawName)
    {
        // Construct ResourceName via the unsafe path: serialize the wire form
        // (a bare string) into a JsonElement and Deserialize<ResourceName>(...)
        // — but the converter throws on invalid input too. Instead use
        // RuntimeHelpers.GetUninitializedObject + reflection to bypass the
        // constructor entirely.
        var nameObj = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ResourceName));
        typeof(ResourceName).GetField("<Value>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            !.SetValue(nameObj, rawName);
        var unsafeName = (ResourceName)nameObj;
        return resource with { Name = unsafeName };
    }

    private sealed class ServiceProviderStub : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
