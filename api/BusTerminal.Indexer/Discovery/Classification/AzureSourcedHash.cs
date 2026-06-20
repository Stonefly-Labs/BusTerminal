using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BusTerminal.Indexer.Discovery.Classification;

// Spec 009 / T050 + R-08. Canonical SHA-256 over the entity's `azureSourced`
// payload. Hashing the *canonical* JSON form (sorted keys, normalized number
// formats, normalized null handling) means the same observation always
// produces the same hash regardless of dictionary insertion order.
//
// The output is prefixed `sha256:` and base64-encoded — matches the format
// the API-side validator expects on the persisted document.
public static class AzureSourcedHash
{
    public const string Prefix = "sha256:";

    public static string Compute(IReadOnlyDictionary<string, object?> azureSourced)
    {
        ArgumentNullException.ThrowIfNull(azureSourced);

        var canonical = ToCanonicalJson(azureSourced);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Prefix + Convert.ToBase64String(bytes);
    }

    internal static string ToCanonicalJson(IReadOnlyDictionary<string, object?> source)
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteSorted(writer, source);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case decimal dec:
                writer.WriteNumberValue(dec);
                break;
            case IReadOnlyDictionary<string, object?> dict:
                WriteSorted(writer, dict);
                break;
            case IEnumerable<object?> arr:
                writer.WriteStartArray();
                foreach (var item in arr)
                {
                    WriteValue(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(value.ToString() ?? string.Empty);
                break;
        }
    }

    private static void WriteSorted(Utf8JsonWriter writer, IReadOnlyDictionary<string, object?> source)
    {
        writer.WriteStartObject();
        foreach (var kv in source.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(kv.Key);
            WriteValue(writer, kv.Value);
        }
        writer.WriteEndObject();
    }
}
