using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-019.
[JsonConverter(typeof(JsonStringEnumConverter<DocumentationAssetKind>))]
public enum DocumentationAssetKind
{
    Runbook,
    Wiki,
    AsyncApiSpec,
    ArchitectureDiagram,
    OperationalGuide,
    ExternalRepository,
}

public sealed record DocumentationReference(DocumentationAssetKind AssetKind, string Uri);
