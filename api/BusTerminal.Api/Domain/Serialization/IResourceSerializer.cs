namespace BusTerminal.Api.Domain.Serialization;

// Spec 004 / FR-016. JSON methods land in Phase 2 (T044/T047); YAML methods land
// in US8 (T141/T142 — YamlResourceSerializer). Interface declares both so the
// dependency surface is stable; JsonResourceSerializer continues to throw on the
// YAML methods (it is not a YAML implementation), while YamlResourceSerializer
// implements both surfaces.
public interface IResourceSerializer
{
    string SerializeToJson(Resource resource);

    Resource DeserializeFromJson(string json);

    string SerializeEnvelopeToJson(ImportExportEnvelope envelope);

    ImportExportEnvelope DeserializeEnvelopeFromJson(string json);

    string SerializeToYaml(Resource resource);

    Resource DeserializeFromYaml(string yaml);

    string SerializeEnvelopeToYaml(ImportExportEnvelope envelope);

    ImportExportEnvelope DeserializeEnvelopeFromYaml(string yaml);
}
