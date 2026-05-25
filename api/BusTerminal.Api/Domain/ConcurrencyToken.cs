using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-025 / Q2. Opaque from the domain perspective; corresponds to Cosmos `_etag`.
[JsonConverter(typeof(ConcurrencyTokenJsonConverter))]
public readonly record struct ConcurrencyToken(string Value)
{
    public static ConcurrencyToken Empty => new(string.Empty);

    public override string ToString() => Value;

    public static implicit operator string(ConcurrencyToken token) => token.Value;
}

internal sealed class ConcurrencyTokenJsonConverter : JsonConverter<ConcurrencyToken>
{
    public override ConcurrencyToken Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
        new(reader.GetString() ?? string.Empty);

    public override void Write(System.Text.Json.Utf8JsonWriter writer, ConcurrencyToken value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
