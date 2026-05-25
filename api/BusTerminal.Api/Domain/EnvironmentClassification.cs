using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-017. Discriminated value: a closed minimum vocabulary plus an open
// custom string. Serialized as a bare string in JSON; known values use the camelCase
// form, custom values pass through.
[JsonConverter(typeof(EnvironmentClassificationJsonConverter))]
public readonly record struct EnvironmentClassification
{
    public static readonly EnvironmentClassification Development = new("development");
    public static readonly EnvironmentClassification Test = new("test");
    public static readonly EnvironmentClassification QA = new("qa");
    public static readonly EnvironmentClassification Staging = new("staging");
    public static readonly EnvironmentClassification Production = new("production");
    public static readonly EnvironmentClassification DisasterRecovery = new("disasterRecovery");

    private static readonly HashSet<string> KnownValues = new(StringComparer.Ordinal)
    {
        "development", "test", "qa", "staging", "production", "disasterRecovery",
    };

    public string Value { get; }

    public EnvironmentClassification(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Value = value;
    }

    public bool IsKnown => KnownValues.Contains(Value);

    public override string ToString() => Value;
}

internal sealed class EnvironmentClassificationJsonConverter : JsonConverter<EnvironmentClassification>
{
    public override EnvironmentClassification Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new JsonException("EnvironmentClassification must be a non-null string."));

    public override void Write(Utf8JsonWriter writer, EnvironmentClassification value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
