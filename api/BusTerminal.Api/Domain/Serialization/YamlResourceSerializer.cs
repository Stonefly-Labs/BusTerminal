using System.Globalization;
using System.Text;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace BusTerminal.Api.Domain.Serialization;

// Spec 004 / FR-016 / T142 (US8). YAML serializer for Resource + ImportExportEnvelope.
//
// Strategy: every YAML operation funnels through the JSON pipeline so the
// polymorphic dispatch, the Extensions converter, the per-type serialization
// shape, and the camelCase property naming are exactly identical to the JSON
// path. We translate between JSON (System.Text.Json's JsonElement) and YAML
// (YamlDotNet's RepresentationModel) tree-to-tree so we control the scalar
// styles directly and never depend on YamlDotNet's implicit type inference.
//
// Scalar-style convention (preserves JSON ↔ YAML round-trip without ambiguity):
// - JSON strings  → YAML scalars in `DoubleQuoted` style. Any string is
//   unambiguously a string on the YAML side regardless of content (so a string
//   that happens to look like `true` or `42` survives the round-trip).
// - JSON numbers  → `Plain` style with the original raw text. We don't reformat
//   the number — `JsonElement.GetRawText()` already gives the canonical
//   serialization.
// - JSON true/false/null → `Plain` literals.
// - JSON objects  → YamlMappingNode.
// - JSON arrays   → YamlSequenceNode.
//
// On the inbound (YAML→JSON) side, the inverse rule fires: quoted scalars are
// strings; plain scalars are parsed as number, bool, or null in that order, and
// fall back to string if none match. Hand-edited YAML that uses plain scalars
// for legitimate strings still works because the resulting JSON string will be
// fed back through the JSON pipeline, where the per-property converter coerces
// it to the right CLR type (e.g., enum strings, custom value types).
public sealed class YamlResourceSerializer : IResourceSerializer
{
    private readonly JsonResourceSerializer _json;

    public YamlResourceSerializer(JsonResourceSerializer json)
    {
        _json = json;
    }

    public string SerializeToJson(Resource resource) => _json.SerializeToJson(resource);

    public Resource DeserializeFromJson(string json) => _json.DeserializeFromJson(json);

    public string SerializeEnvelopeToJson(ImportExportEnvelope envelope) =>
        _json.SerializeEnvelopeToJson(envelope);

    public ImportExportEnvelope DeserializeEnvelopeFromJson(string json) =>
        _json.DeserializeEnvelopeFromJson(json);

    public string SerializeToYaml(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return ConvertJsonToYaml(_json.SerializeToJson(resource));
    }

    public Resource DeserializeFromYaml(string yaml)
    {
        ArgumentException.ThrowIfNullOrEmpty(yaml);
        return _json.DeserializeFromJson(ConvertYamlToJson(yaml));
    }

    public string SerializeEnvelopeToYaml(ImportExportEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return ConvertJsonToYaml(_json.SerializeEnvelopeToJson(envelope));
    }

    public ImportExportEnvelope DeserializeEnvelopeFromYaml(string yaml)
    {
        ArgumentException.ThrowIfNullOrEmpty(yaml);
        return _json.DeserializeEnvelopeFromJson(ConvertYamlToJson(yaml));
    }

    private static string ConvertJsonToYaml(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = JsonElementToYamlNode(doc.RootElement);
        var stream = new YamlStream(new YamlDocument(root));

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        stream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    private static string ConvertYamlToJson(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0)
        {
            throw new InvalidDataException("YAML input contains no documents.");
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            YamlNodeToJson(stream.Documents[0].RootNode, writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static YamlNode JsonElementToYamlNode(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var mapping = new YamlMappingNode();
                foreach (var property in element.EnumerateObject())
                {
                    mapping.Add(
                        new YamlScalarNode(property.Name) { Style = ScalarStyle.DoubleQuoted },
                        JsonElementToYamlNode(property.Value));
                }

                return mapping;

            case JsonValueKind.Array:
                var sequence = new YamlSequenceNode();
                foreach (var item in element.EnumerateArray())
                {
                    sequence.Add(JsonElementToYamlNode(item));
                }

                return sequence;

            case JsonValueKind.String:
                return new YamlScalarNode(element.GetString() ?? string.Empty)
                {
                    Style = ScalarStyle.DoubleQuoted,
                };

            case JsonValueKind.Number:
                return new YamlScalarNode(element.GetRawText()) { Style = ScalarStyle.Plain };

            case JsonValueKind.True:
                return new YamlScalarNode("true") { Style = ScalarStyle.Plain };

            case JsonValueKind.False:
                return new YamlScalarNode("false") { Style = ScalarStyle.Plain };

            case JsonValueKind.Null:
                return new YamlScalarNode("null") { Style = ScalarStyle.Plain };

            default:
                throw new InvalidOperationException(
                    $"Unhandled JsonValueKind '{element.ValueKind}' encountered during YAML conversion.");
        }
    }

    private static void YamlNodeToJson(YamlNode node, Utf8JsonWriter writer)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                writer.WriteStartObject();
                foreach (var (key, value) in mapping.Children)
                {
                    if (key is not YamlScalarNode scalarKey)
                    {
                        throw new InvalidDataException(
                            "YAML mapping keys must be scalar — non-scalar keys are not part of the JSON-compatible subset.");
                    }

                    writer.WritePropertyName(scalarKey.Value ?? string.Empty);
                    YamlNodeToJson(value, writer);
                }

                writer.WriteEndObject();
                break;

            case YamlSequenceNode sequence:
                writer.WriteStartArray();
                foreach (var item in sequence.Children)
                {
                    YamlNodeToJson(item, writer);
                }

                writer.WriteEndArray();
                break;

            case YamlScalarNode scalar:
                WriteScalarAsJson(scalar, writer);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unhandled YAML node type '{node.GetType().Name}' encountered during JSON conversion.");
        }
    }

    private static void WriteScalarAsJson(YamlScalarNode scalar, Utf8JsonWriter writer)
    {
        // Quoted scalars are always strings. Plain scalars are resolved via the
        // narrow JSON-compatible subset (null | bool | number | string).
        var raw = scalar.Value ?? string.Empty;

        if (scalar.Style is ScalarStyle.DoubleQuoted or ScalarStyle.SingleQuoted)
        {
            writer.WriteStringValue(raw);
            return;
        }

        // Plain / Folded / Literal styles — type-infer for round-trip with the
        // JSON shape. We deliberately only resolve the JSON-compatible subset
        // here; arbitrary YAML tags (`!!timestamp`, `!!binary`, …) are out of
        // scope per the spec — those would fall through to string.
        if (raw.Equals("null", StringComparison.Ordinal) || raw.Length == 0)
        {
            writer.WriteNullValue();
            return;
        }

        if (raw.Equals("true", StringComparison.Ordinal))
        {
            writer.WriteBooleanValue(true);
            return;
        }

        if (raw.Equals("false", StringComparison.Ordinal))
        {
            writer.WriteBooleanValue(false);
            return;
        }

        if (TryWriteNumber(raw, writer))
        {
            return;
        }

        writer.WriteStringValue(raw);
    }

    private static bool TryWriteNumber(string raw, Utf8JsonWriter writer)
    {
        // We only treat the strict JSON number grammar as a number. YAML's wider
        // numeric grammar (sexagesimal, hex, infinities) is intentionally not
        // supported — those values surface as strings, which is the safer choice
        // on this path.
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asLong))
        {
            writer.WriteNumberValue(asLong);
            return true;
        }

        if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var asDecimal))
        {
            writer.WriteNumberValue(asDecimal);
            return true;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var asDouble))
        {
            writer.WriteNumberValue(asDouble);
            return true;
        }

        return false;
    }
}
