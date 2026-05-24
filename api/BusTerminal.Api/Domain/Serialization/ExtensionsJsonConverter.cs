using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain.Serialization;

// Spec 004 / FR-012. Preserves structured JSON values intact via JsonElement.Clone()
// so nested objects, arrays, and primitives all round-trip without re-serialization.
public sealed class ExtensionsJsonConverter : JsonConverter<Domain.Extensions>
{
    public override Domain.Extensions Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Domain.Extensions.Empty;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Extensions must be a JSON object.");
        }

        var entries = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        using var document = JsonDocument.ParseValue(ref reader);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            // Clone — JsonDocument is disposed at the end of the using; the cloned
            // JsonElement is independent and safe to outlive it.
            entries[property.Name] = property.Value.Clone();
        }

        return new Domain.Extensions(entries);
    }

    public override void Write(
        Utf8JsonWriter writer,
        Domain.Extensions value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var entry in value)
        {
            writer.WritePropertyName(entry.Key);
            entry.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
