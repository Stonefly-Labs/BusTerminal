namespace BusTerminal.Api.Domain.Serialization;

// Spec 004 / FR-016. JSON methods land in Phase 2 (T044/T047); YAML methods land
// in US8 (T141). Interface declares both so the dependency surface is stable.
public interface IResourceSerializer
{
    string SerializeToJson(Resource resource);

    Resource DeserializeFromJson(string json);

    string SerializeEnvelopeToJson(ImportExportEnvelope envelope);

    ImportExportEnvelope DeserializeEnvelopeFromJson(string json);

    string SerializeToYaml(Resource resource) =>
        throw new NotSupportedException("YAML serialization lands in US8 (T141). JsonResourceSerializer does not implement YAML.");

    Resource DeserializeFromYaml(string yaml) =>
        throw new NotSupportedException("YAML serialization lands in US8 (T141). JsonResourceSerializer does not implement YAML.");

    string SerializeEnvelopeToYaml(ImportExportEnvelope envelope) =>
        throw new NotSupportedException("YAML serialization lands in US8 (T141). JsonResourceSerializer does not implement YAML.");

    ImportExportEnvelope DeserializeEnvelopeFromYaml(string yaml) =>
        throw new NotSupportedException("YAML serialization lands in US8 (T141). JsonResourceSerializer does not implement YAML.");
}
