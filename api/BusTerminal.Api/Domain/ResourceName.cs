using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-022. Lowercase, hyphen-separated, no spaces. Same pattern as the
// JSON schema's `name` property in canonical-resource.schema.json.
[JsonConverter(typeof(ResourceNameJsonConverter))]
public readonly record struct ResourceName
{
    private static readonly Regex Pattern = new(
        "^[a-z0-9]+(-[a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Value { get; }

    public ResourceName(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        if (!Pattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"ResourceName must match {Pattern} (lowercase, hyphen-separated, no spaces). Got: '{value}'.",
                nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;

    public static implicit operator string(ResourceName name) => name.Value;
}

internal sealed class ResourceNameJsonConverter : JsonConverter<ResourceName>
{
    public override ResourceName Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new System.Text.Json.JsonException("ResourceName must be a non-null string."));

    public override void Write(System.Text.Json.Utf8JsonWriter writer, ResourceName value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
