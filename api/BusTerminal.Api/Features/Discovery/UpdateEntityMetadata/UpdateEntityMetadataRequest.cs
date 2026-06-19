using System.Text.Json;
using System.Text.Json.Serialization;
using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.UpdateEntityMetadata;

// Spec 009 / T103 + contracts/openapi.yaml#updateEntityMetadata.
// Request body for PATCH /api/entities/{entityId}.
//
// JsonElement properties are used so the endpoint can distinguish between
// "field is absent" (ValueKind == Undefined → no change) and "field is
// explicitly null" (ValueKind == Null → clear). System.Text.Json defaults
// any property not present in the JSON to default(JsonElement), whose
// ValueKind is Undefined — exactly the signal we want.
//
// The Validator rejects unexpected keys (specifically azureSourced.*) at
// the body-extra-data layer; FluentValidation runs against the typed shape.
public sealed record UpdateEntityMetadataRequest
{
    [JsonPropertyName("description")] public JsonElement Description { get; init; }
    [JsonPropertyName("businessPurpose")] public JsonElement BusinessPurpose { get; init; }
    [JsonPropertyName("tags")] public JsonElement Tags { get; init; }
    [JsonPropertyName("documentationLinks")] public JsonElement DocumentationLinks { get; init; }
    [JsonPropertyName("contactInformation")] public JsonElement ContactInformation { get; init; }
    [JsonPropertyName("operationalNotes")] public JsonElement OperationalNotes { get; init; }
}

// Spec 009 / T103. Parsed contact-information block (when ContactInformation
// is present and non-null). Mirrors EntityContactInformation but lives in
// the slice's namespace to keep the wire shape self-contained.
public sealed record UpdateEntityMetadataContactBlock(string? PrimaryContact, string? EscalationPath);

// Spec 009 / T103. Parsed documentation-link entry. Stored verbatim into
// `EntityDocumentationLink` after validation.
public sealed record UpdateEntityMetadataDocLink(string Label, string Url);
