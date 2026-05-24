using System.Text.Json;
using BusTerminal.Api.Domain;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Serialization;

// Spec 004 / T089 / Q4 + FR-012. Structured extension values + __indexable
// marker survive round-trip.
public sealed class ExtensionPreservationTests : IDisposable
{
    private readonly SerializerFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Structured_extension_value_round_trips_intact()
    {
        var nested = JsonDocument.Parse("{\"sla\":\"99.9\",\"target\":{\"latencyMs\":50,\"errorRate\":0.001}}").RootElement;
        var indexable = JsonDocument.Parse("{\"contoso:costCenter\":true,\"contoso:sla\":false}").RootElement;
        var primitive = JsonDocument.Parse("\"FIN-102\"").RootElement;

        var entries = new Dictionary<string, JsonElement>
        {
            ["contoso:sla"] = nested,
            ["contoso:costCenter"] = primitive,
            ["__indexable"] = indexable,
        };

        var queue = ResourceFactory.BuildQueue() with
        {
            Extensions = new Extensions(entries),
        };

        var json = _fixture.Serializer.SerializeToJson(queue);
        var roundTripped = _fixture.Serializer.DeserializeFromJson(json);

        roundTripped.Extensions.Should().ContainKey("contoso:sla");
        roundTripped.Extensions["contoso:sla"].GetProperty("sla").GetString().Should().Be("99.9");
        roundTripped.Extensions["contoso:sla"].GetProperty("target").GetProperty("latencyMs").GetInt32().Should().Be(50);

        roundTripped.Extensions["contoso:costCenter"].GetString().Should().Be("FIN-102");

        roundTripped.Extensions.Should().ContainKey("__indexable");
        roundTripped.Extensions["__indexable"].GetProperty("contoso:costCenter").GetBoolean().Should().BeTrue();
        roundTripped.Extensions["__indexable"].GetProperty("contoso:sla").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Malformed_key_throws_at_construction()
    {
        var entries = new Dictionary<string, JsonElement>
        {
            ["NOT-NAMESPACED"] = JsonDocument.Parse("\"x\"").RootElement,
        };

        var act = () => new Extensions(entries);
        act.Should().Throw<ArgumentException>();
    }
}
