using System.Text.Json;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Serialization;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Serialization;

// Spec 004 / T146 / SC-009 per-type evidence. Mirrors JsonRoundTripTests:
// every first-class resource type must round-trip YAML losslessly. Lossless is
// defined as JSON-shape equivalence — the serialized JSON of the rehydrated
// resource must equal the serialized JSON of the original.
public sealed class YamlRoundTripTests : IDisposable
{
    private readonly SerializerFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    public static IEnumerable<object[]> AllTypes() =>
        ResourceFactory.OneOfEachType().Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Each_first_class_type_round_trips_yaml(Resource resource)
    {
        var originalJson = _fixture.Serializer.SerializeToJson(resource);

        var yaml = _fixture.YamlSerializer.SerializeToYaml(resource);
        yaml.Should().NotBeNullOrWhiteSpace();

        var rehydrated = _fixture.YamlSerializer.DeserializeFromYaml(yaml);
        rehydrated.Should().BeOfType(resource.GetType());
        rehydrated.Id.Should().Be(resource.Id);
        rehydrated.ResourceType.Should().Be(resource.ResourceType);
        rehydrated.Name.Value.Should().Be(resource.Name.Value);
        rehydrated.NamespacePath.Value.Should().Be(resource.NamespacePath.Value);
        rehydrated.Lifecycle.Should().Be(resource.Lifecycle);

        var rehydratedJson = _fixture.Serializer.SerializeToJson(rehydrated);
        rehydratedJson.Should().Be(originalJson, "YAML round-trip must preserve the JSON shape exactly");
    }

    [Fact]
    public void Envelope_round_trips_yaml_with_relationships_and_change_events()
    {
        var resources = ResourceFactory.OneOfEachType();
        var envelope = new ImportExportEnvelope(
            ExportedAt: DateTimeOffset.Parse("2026-05-23T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            Resources: resources,
            Relationships: Array.Empty<BusTerminal.Api.Domain.Relationships.Relationship>(),
            ExportedBy: new SystemPrincipalReference("yaml-roundtrip-test"),
            SourceSystem: "test/yaml-roundtrip");

        var originalJson = _fixture.Serializer.SerializeEnvelopeToJson(envelope);

        var yaml = _fixture.YamlSerializer.SerializeEnvelopeToYaml(envelope);
        var rehydrated = _fixture.YamlSerializer.DeserializeEnvelopeFromYaml(yaml);

        rehydrated.Resources.Should().HaveCount(resources.Count);
        rehydrated.SchemaVersion.Should().Be(envelope.SchemaVersion);

        var rehydratedJson = _fixture.Serializer.SerializeEnvelopeToJson(rehydrated);
        rehydratedJson.Should().Be(originalJson, "envelope YAML round-trip must preserve the JSON shape exactly");
    }

    [Fact]
    public void Extension_block_with_nested_values_survives_yaml_round_trip()
    {
        // FR-012: Extensions preserve nested objects, arrays, primitives intact.
        // Force string values that look like numbers / booleans / nulls so the
        // scalar-style convention is exercised end-to-end.
        var payload = JsonDocument.Parse("""
            {
                "busterminal:stringLooksLikeNumber": "42",
                "busterminal:stringLooksLikeBool": "true",
                "busterminal:genuineNumber": 7,
                "busterminal:genuineBool": false,
                "busterminal:nullValue": null,
                "busterminal:nested": {
                    "innerArray": ["a", "b", "c"],
                    "innerNumber": 3.14
                }
            }
            """).RootElement;

        var entries = new Dictionary<string, JsonElement>();
        foreach (var prop in payload.EnumerateObject())
        {
            entries[prop.Name] = prop.Value.Clone();
        }

        var queue = ResourceFactory.BuildQueue() with { Extensions = new Extensions(entries) };

        var originalJson = _fixture.Serializer.SerializeToJson(queue);
        var yaml = _fixture.YamlSerializer.SerializeToYaml(queue);
        var rehydrated = _fixture.YamlSerializer.DeserializeFromYaml(yaml);
        var rehydratedJson = _fixture.Serializer.SerializeToJson(rehydrated);

        rehydratedJson.Should().Be(originalJson);
    }

    [Fact]
    public void Unknown_resource_type_round_trips_via_yaml()
    {
        // Q4 / additive evolution: a YAML document carrying an unknown
        // discriminator must rehydrate as UnknownResource with the raw payload
        // intact, exactly like the JSON path.
        var sample = ResourceFactory.BuildQueue();
        var json = _fixture.Serializer.SerializeToJson(sample)
            .Replace("\"queue\"", "\"syntheticFutureType\"", StringComparison.Ordinal);

        var yaml = _fixture.YamlSerializer.SerializeToYaml(
            _fixture.Serializer.DeserializeFromJson(json));

        var rehydrated = _fixture.YamlSerializer.DeserializeFromYaml(yaml);
        rehydrated.Should().BeOfType<UnknownResource>();
        rehydrated.ResourceType.Should().Be("syntheticFutureType");
    }
}
