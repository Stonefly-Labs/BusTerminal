using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain.Serialization;

// Spec 004 / FR-016. The single source of truth for STJ options across the API:
// the persistence layer reuses the same options via CosmosClientOptions.Serializer
// (CosmosStjSerializer) so storage round-trip and import/export round-trip behave
// identically. PropertyNamingPolicy is camelCase to match every JSON Schema in
// contracts/. ReadCommentHandling.Skip + AllowTrailingCommas tolerate hand-edited
// fixture files.
public sealed class JsonResourceSerializer : IResourceSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonResourceSerializer(
        ResourceJsonConverter resourceConverter,
        ExtensionsJsonConverter extensionsConverter)
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = null, // Extension keys are vendor-namespaced — never camelCase-rewritten.
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        _options.Converters.Add(resourceConverter);
        _options.Converters.Add(extensionsConverter);
    }

    public JsonSerializerOptions Options => _options;

    public string SerializeToJson(Resource resource) =>
        JsonSerializer.Serialize(resource, _options);

    public Resource DeserializeFromJson(string json) =>
        JsonSerializer.Deserialize<Resource>(json, _options)
        ?? throw new JsonException("Deserialization yielded null.");

    public string SerializeEnvelopeToJson(ImportExportEnvelope envelope) =>
        JsonSerializer.Serialize(envelope, _options);

    public ImportExportEnvelope DeserializeEnvelopeFromJson(string json) =>
        JsonSerializer.Deserialize<ImportExportEnvelope>(json, _options)
        ?? throw new JsonException("Envelope deserialization yielded null.");

    // YAML methods belong to YamlResourceSerializer (T142). Resolving them
    // through the JSON serializer is a wiring bug — surface it loudly rather
    // than silently emitting JSON.
    public string SerializeToYaml(Resource resource) =>
        throw new NotSupportedException("JsonResourceSerializer does not implement YAML. Resolve YamlResourceSerializer instead (Spec 004 / T142).");

    public Resource DeserializeFromYaml(string yaml) =>
        throw new NotSupportedException("JsonResourceSerializer does not implement YAML. Resolve YamlResourceSerializer instead (Spec 004 / T142).");

    public string SerializeEnvelopeToYaml(ImportExportEnvelope envelope) =>
        throw new NotSupportedException("JsonResourceSerializer does not implement YAML. Resolve YamlResourceSerializer instead (Spec 004 / T142).");

    public ImportExportEnvelope DeserializeEnvelopeFromYaml(string yaml) =>
        throw new NotSupportedException("JsonResourceSerializer does not implement YAML. Resolve YamlResourceSerializer instead (Spec 004 / T142).");
}
