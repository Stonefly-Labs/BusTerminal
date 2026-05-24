using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-021. Strongly-typed wrapper around Guid so callers cannot accidentally
// swap a resource id with a tenant id, principal id, etc. JSON shape is a bare string.
[JsonConverter(typeof(ResourceIdJsonConverter))]
public readonly record struct ResourceId(Guid Value)
{
    public static ResourceId New() => new(Guid.NewGuid());

    public static ResourceId Parse(string value) =>
        new(Guid.Parse(value));

    public static bool TryParse(string? value, [NotNullWhen(true)] out ResourceId? result)
    {
        if (Guid.TryParse(value, out var guid))
        {
            result = new ResourceId(guid);
            return true;
        }

        result = null;
        return false;
    }

    public override string ToString() => Value.ToString("D");

    public static implicit operator Guid(ResourceId id) => id.Value;
}

internal sealed class ResourceIdJsonConverter : System.Text.Json.Serialization.JsonConverter<ResourceId>
{
    public override ResourceId Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var raw = reader.GetString() ?? throw new System.Text.Json.JsonException("ResourceId must be a non-null string.");
        return ResourceId.Parse(raw);
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, ResourceId value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
