using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Serialization;

// Spec 004 / T088 / Q4. Polymorphic deserialization across the 14 known types,
// plus the UnknownResource fallback for unknown discriminators.
public sealed class PolymorphismTests : IDisposable
{
    private readonly SerializerFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Discriminator_drives_concrete_type_selection()
    {
        foreach (var resource in ResourceFactory.OneOfEachType())
        {
            var json = _fixture.Serializer.SerializeToJson(resource);
            var roundTripped = _fixture.Serializer.DeserializeFromJson(json);
            roundTripped.GetType().Should().Be(resource.GetType());
        }
    }

    [Fact]
    public void Unknown_discriminator_falls_through_to_UnknownResource()
    {
        // Build a synthetic JSON document with an unknown discriminator but with
        // the full base shape so factory base-field extraction succeeds.
        var sample = ResourceFactory.BuildQueue();
        var json = _fixture.Serializer.SerializeToJson(sample);
        var mutated = json.Replace("\"queue\"", "\"syntheticFutureType\"", StringComparison.Ordinal);

        var deserialized = _fixture.Serializer.DeserializeFromJson(mutated);

        deserialized.Should().BeOfType<UnknownResource>();
        deserialized.ResourceType.Should().Be("syntheticFutureType");
        ((UnknownResource)deserialized).RawJson.GetProperty("resourceType").GetString()
            .Should().Be("syntheticFutureType");
    }
}
