using System.Text.Json;
using BusTerminal.Api.Domain;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Domain;

// Spec 004 / T135 / FR-012. The Extensions type's value-level contract:
// namespaced-key construction invariants, structured + primitive value
// preservation, multi-vendor coexistence, and the reserved __indexable
// marker.
//
// JSON round-trip is exercised separately by
// `Unit/Serialization/ExtensionPreservationTests` — this suite focuses on the
// Extensions value type itself so the contract is verifiable without the
// serializer.
public sealed class ExtensionsTests
{
    [Fact]
    public void Structured_object_value_is_preserved_intact()
    {
        var nested = JsonDocument.Parse("""{"target":"99.9","windowDays":30,"incidentResponseMinutes":15}""").RootElement.Clone();
        var entries = new Dictionary<string, JsonElement>
        {
            ["contoso:sla"] = nested,
        };

        var extensions = new Extensions(entries);

        extensions.Should().ContainKey("contoso:sla");
        extensions["contoso:sla"].GetProperty("target").GetString().Should().Be("99.9");
        extensions["contoso:sla"].GetProperty("windowDays").GetInt32().Should().Be(30);
        extensions["contoso:sla"].GetProperty("incidentResponseMinutes").GetInt32().Should().Be(15);
    }

    [Fact]
    public void Primitive_value_is_preserved_intact()
    {
        var primitive = JsonDocument.Parse("\"FIN-102\"").RootElement.Clone();
        var entries = new Dictionary<string, JsonElement>
        {
            ["contoso:costCenter"] = primitive,
        };

        var extensions = new Extensions(entries);

        extensions["contoso:costCenter"].ValueKind.Should().Be(JsonValueKind.String);
        extensions["contoso:costCenter"].GetString().Should().Be("FIN-102");
    }

    [Fact]
    public void Multiple_vendors_with_same_suffix_coexist()
    {
        var contosoSla = JsonDocument.Parse("\"contoso-strict\"").RootElement.Clone();
        var fabrikamSla = JsonDocument.Parse("\"fabrikam-lenient\"").RootElement.Clone();
        var entries = new Dictionary<string, JsonElement>
        {
            ["contoso:sla"] = contosoSla,
            ["fabrikam:sla"] = fabrikamSla,
        };

        var extensions = new Extensions(entries);

        extensions.Count.Should().Be(2);
        extensions["contoso:sla"].GetString().Should().Be("contoso-strict");
        extensions["fabrikam:sla"].GetString().Should().Be("fabrikam-lenient");
    }

    [Fact]
    public void Indexable_marker_is_preserved_with_per_key_booleans()
    {
        var indexable = JsonDocument.Parse("""{"contoso:costCenter":true,"contoso:sla":false}""").RootElement.Clone();
        var entries = new Dictionary<string, JsonElement>
        {
            ["contoso:costCenter"] = JsonDocument.Parse("\"FIN-102\"").RootElement.Clone(),
            ["contoso:sla"] = JsonDocument.Parse("\"99.9\"").RootElement.Clone(),
            [Extensions.IndexableMarkerKey] = indexable,
        };

        var extensions = new Extensions(entries);

        extensions.Should().ContainKey(Extensions.IndexableMarkerKey);
        extensions[Extensions.IndexableMarkerKey].GetProperty("contoso:costCenter").GetBoolean().Should().BeTrue();
        extensions[Extensions.IndexableMarkerKey].GetProperty("contoso:sla").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Constructor_rejects_malformed_key()
    {
        var entries = new Dictionary<string, JsonElement>
        {
            ["NOT-NAMESPACED"] = JsonDocument.Parse("\"x\"").RootElement.Clone(),
        };

        var act = () => new Extensions(entries);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*NOT-NAMESPACED*namespaced*");
    }

    [Fact]
    public void Constructor_rejects_uppercase_vendor_prefix()
    {
        var entries = new Dictionary<string, JsonElement>
        {
            ["Contoso:costCenter"] = JsonDocument.Parse("\"x\"").RootElement.Clone(),
        };

        var act = () => new Extensions(entries);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsValidKey_accepts_namespaced_form_and_indexable_marker()
    {
        Extensions.IsValidKey("contoso:costCenter").Should().BeTrue();
        Extensions.IsValidKey("fabrikam:onCallRotation").Should().BeTrue();
        Extensions.IsValidKey("contoso-platform:tag.subtag").Should().BeTrue();
        Extensions.IsValidKey(Extensions.IndexableMarkerKey).Should().BeTrue();
    }

    [Fact]
    public void IsValidKey_rejects_unnamespaced_and_malformed_forms()
    {
        Extensions.IsValidKey("costCenter").Should().BeFalse();
        Extensions.IsValidKey(":costCenter").Should().BeFalse();
        Extensions.IsValidKey("contoso:").Should().BeFalse();
        Extensions.IsValidKey("Contoso:costCenter").Should().BeFalse();
        Extensions.IsValidKey("contoso costCenter").Should().BeFalse();
    }

    [Fact]
    public void Empty_returns_singleton_with_zero_entries()
    {
        Extensions.Empty.Count.Should().Be(0);
        Extensions.Empty.Keys.Should().BeEmpty();
    }
}
