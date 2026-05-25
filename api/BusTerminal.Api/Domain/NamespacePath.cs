using System.Collections.Immutable;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-003. Hierarchical, slash-separated path.
// Same segment regex as ResourceName.
[JsonConverter(typeof(NamespacePathJsonConverter))]
public readonly record struct NamespacePath
{
    private static readonly Regex SegmentPattern = new(
        "^[a-z0-9]+(-[a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Value { get; }

    public NamespacePath(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        foreach (var segment in value.Split('/'))
        {
            if (!SegmentPattern.IsMatch(segment))
            {
                throw new ArgumentException(
                    $"NamespacePath segment '{segment}' must match {SegmentPattern} (lowercase, hyphen-separated, no spaces). Full path: '{value}'.",
                    nameof(value));
            }
        }

        Value = value;
    }

    public ImmutableArray<string> Segments => [.. Value.Split('/')];

    public bool IsRoot => !Value.Contains('/');

    public NamespacePath? Parent
    {
        get
        {
            var lastSlash = Value.LastIndexOf('/');
            return lastSlash < 0 ? null : new NamespacePath(Value[..lastSlash]);
        }
    }

    public NamespacePath Append(string segment) => new($"{Value}/{segment}");

    public override string ToString() => Value;

    public static implicit operator string(NamespacePath path) => path.Value;
}

internal sealed class NamespacePathJsonConverter : JsonConverter<NamespacePath>
{
    public override NamespacePath Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new System.Text.Json.JsonException("NamespacePath must be a non-null string."));

    public override void Write(System.Text.Json.Utf8JsonWriter writer, NamespacePath value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
