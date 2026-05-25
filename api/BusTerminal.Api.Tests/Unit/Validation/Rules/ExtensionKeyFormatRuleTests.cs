using System.Reflection;
using System.Text.Json;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Domain.Validation.Rules;
using BusTerminal.Api.Tests.Unit.Serialization;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Validation.Rules;

// Spec 004 / T136 / FR-012.
public sealed class ExtensionKeyFormatRuleTests
{
    private static ValidationContext EmptyContext() => new()
    {
        RelationshipResolver = _ => null,
        DuplicateDetector = _ => false,
        Services = new ServiceProviderStub(),
        PreviousLifecycle = null,
    };

    [Fact]
    public void Namespaced_keys_produce_no_findings()
    {
        var entries = new Dictionary<string, JsonElement>
        {
            ["contoso:costCenter"] = JsonDocument.Parse("\"FIN-102\"").RootElement.Clone(),
            ["fabrikam:onCallRotation"] = JsonDocument.Parse("\"team-alpha\"").RootElement.Clone(),
        };
        var resource = ResourceFactory.BuildQueue() with { Extensions = new Extensions(entries) };

        var rule = new ExtensionKeyFormatRule(TimeProvider.System);
        var findings = rule.Validate(resource, EmptyContext()).ToList();

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Indexable_marker_is_ignored_by_the_rule()
    {
        var entries = new Dictionary<string, JsonElement>
        {
            ["contoso:costCenter"] = JsonDocument.Parse("\"FIN-102\"").RootElement.Clone(),
            [Extensions.IndexableMarkerKey] = JsonDocument.Parse("""{"contoso:costCenter":true}""").RootElement.Clone(),
        };
        var resource = ResourceFactory.BuildQueue() with { Extensions = new Extensions(entries) };

        var rule = new ExtensionKeyFormatRule(TimeProvider.System);
        var findings = rule.Validate(resource, EmptyContext()).ToList();

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Malformed_key_via_bypass_path_fires_warning()
    {
        // The Extensions constructor blocks malformed keys, so simulate the
        // future-ingest-path scenario by constructing Extensions with valid
        // keys then mutating the private dictionary to inject a malformed key.
        var goodValue = JsonDocument.Parse("\"x\"").RootElement.Clone();
        var extensions = new Extensions(new Dictionary<string, JsonElement>
        {
            ["contoso:placeholder"] = goodValue,
        });
        InjectKey(extensions, "NOT-NAMESPACED", goodValue);

        var resource = ResourceFactory.BuildQueue() with { Extensions = extensions };
        var rule = new ExtensionKeyFormatRule(TimeProvider.System);

        var findings = rule.Validate(resource, EmptyContext()).ToList();

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Warning);
        findings[0].RuleId.Should().Be(ExtensionKeyFormatRule.RuleId);
        findings[0].FieldRef.Should().Be("/extensions/NOT-NAMESPACED");
    }

    [Fact]
    public void Multiple_malformed_keys_each_fire_their_own_finding()
    {
        var goodValue = JsonDocument.Parse("\"x\"").RootElement.Clone();
        var extensions = new Extensions(new Dictionary<string, JsonElement>
        {
            ["contoso:placeholder"] = goodValue,
        });
        InjectKey(extensions, "first-bad", goodValue);
        InjectKey(extensions, "Second:Bad", goodValue);

        var resource = ResourceFactory.BuildQueue() with { Extensions = extensions };
        var rule = new ExtensionKeyFormatRule(TimeProvider.System);

        var findings = rule.Validate(resource, EmptyContext()).ToList();

        findings.Should().HaveCount(2);
        findings.Should().OnlyContain(f => f.Severity == ValidationSeverity.Warning);
        findings.Select(f => f.FieldRef).Should().BeEquivalentTo(new[]
        {
            "/extensions/first-bad",
            "/extensions/Second:Bad",
        });
    }

    private static void InjectKey(Extensions extensions, string key, JsonElement value)
    {
        // The constructor stores entries as the same IReadOnlyDictionary instance
        // it was handed, but accessed through the read-only interface. Bypass via
        // reflection on the private backing field; replace it with a fresh
        // Dictionary that contains the injected (malformed) key.
        var field = typeof(Extensions).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Extensions._entries field not found.");
        var current = (IReadOnlyDictionary<string, JsonElement>)field.GetValue(extensions)!;
        var mutable = new Dictionary<string, JsonElement>(current, StringComparer.Ordinal)
        {
            [key] = value,
        };
        field.SetValue(extensions, mutable);
    }

    private sealed class ServiceProviderStub : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
