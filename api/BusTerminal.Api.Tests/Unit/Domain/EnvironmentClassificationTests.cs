using System.Text.Json;
using BusTerminal.Api.Domain;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Domain;

// Spec 004 / T139 / FR-017. EnvironmentClassification is a discriminated value:
// a closed minimum vocabulary (development/test/qa/staging/production/disasterRecovery)
// plus an open custom string. JSON wire form is a bare string in both cases.
public sealed class EnvironmentClassificationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static TheoryData<EnvironmentClassification, string> KnownCases() => new()
    {
        { EnvironmentClassification.Development, "development" },
        { EnvironmentClassification.Test, "test" },
        { EnvironmentClassification.QA, "qa" },
        { EnvironmentClassification.Staging, "staging" },
        { EnvironmentClassification.Production, "production" },
        { EnvironmentClassification.DisasterRecovery, "disasterRecovery" },
    };

    [Theory]
    [MemberData(nameof(KnownCases))]
    public void Known_case_serializes_as_camelCase_bare_string(EnvironmentClassification env, string expectedWire)
    {
        var json = JsonSerializer.Serialize(env, Options);

        json.Should().Be($"\"{expectedWire}\"");
    }

    [Theory]
    [MemberData(nameof(KnownCases))]
    public void Known_case_round_trips(EnvironmentClassification env, string expectedWire)
    {
        var json = JsonSerializer.Serialize(env, Options);
        var deserialized = JsonSerializer.Deserialize<EnvironmentClassification>(json, Options);

        deserialized.Value.Should().Be(expectedWire);
        deserialized.IsKnown.Should().BeTrue();
        deserialized.Should().Be(env);
    }

    [Fact]
    public void Custom_case_serializes_as_raw_string()
    {
        var custom = new EnvironmentClassification("training");

        var json = JsonSerializer.Serialize(custom, Options);

        json.Should().Be("\"training\"");
    }

    [Fact]
    public void Custom_case_round_trips_and_is_marked_unknown()
    {
        var custom = new EnvironmentClassification("training");

        var json = JsonSerializer.Serialize(custom, Options);
        var deserialized = JsonSerializer.Deserialize<EnvironmentClassification>(json, Options);

        deserialized.Value.Should().Be("training");
        deserialized.IsKnown.Should().BeFalse();
        deserialized.Should().Be(custom);
    }

    [Fact]
    public void Array_of_mixed_known_and_custom_values_round_trips()
    {
        var input = new[]
        {
            EnvironmentClassification.Production,
            EnvironmentClassification.DisasterRecovery,
            new EnvironmentClassification("training"),
        };

        var json = JsonSerializer.Serialize(input, Options);
        var roundTripped = JsonSerializer.Deserialize<EnvironmentClassification[]>(json, Options);

        roundTripped.Should().NotBeNull();
        roundTripped!.Should().HaveCount(3);
        roundTripped[0].Value.Should().Be("production");
        roundTripped[1].Value.Should().Be("disasterRecovery");
        roundTripped[2].Value.Should().Be("training");
        roundTripped[2].IsKnown.Should().BeFalse();
    }

    [Fact]
    public void Construction_with_empty_string_throws()
    {
        var act = () => new EnvironmentClassification("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construction_with_null_throws()
    {
        var act = () => new EnvironmentClassification(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_is_value_based_across_known_and_custom_constructors()
    {
        var fromConstant = EnvironmentClassification.Production;
        var fromCustomConstructor = new EnvironmentClassification("production");

        fromCustomConstructor.Should().Be(fromConstant);
        fromCustomConstructor.IsKnown.Should().BeTrue();
    }
}
