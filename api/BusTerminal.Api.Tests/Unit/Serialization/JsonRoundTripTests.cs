using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Serialization;

// Spec 004 / T087 / SC-001. Every first-class type round-trips JSON losslessly.
public sealed class JsonRoundTripTests : IDisposable
{
    private readonly SerializerFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    public static IEnumerable<object[]> AllTypes() =>
        ResourceFactory.OneOfEachType().Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Each_first_class_type_round_trips(Resource resource)
    {
        var json = _fixture.Serializer.SerializeToJson(resource);
        var rehydrated = _fixture.Serializer.DeserializeFromJson(json);

        rehydrated.Should().BeOfType(resource.GetType());
        rehydrated.Id.Should().Be(resource.Id);
        rehydrated.ResourceType.Should().Be(resource.ResourceType);
        rehydrated.Name.Value.Should().Be(resource.Name.Value);
        rehydrated.NamespacePath.Value.Should().Be(resource.NamespacePath.Value);
        rehydrated.Lifecycle.Should().Be(resource.Lifecycle);

        // Per-type fields preserved — compare JSON bodies rather than record
        // equality (Extensions is a class and IReadOnlyCollection<T> fields do
        // not implement structural equality; equality semantics on the domain
        // records would require IEqualityComparer plumbing that the spec does
        // not currently call for).
        var serializedAgain = _fixture.Serializer.SerializeToJson(rehydrated);
        serializedAgain.Should().Be(json);
    }
}
