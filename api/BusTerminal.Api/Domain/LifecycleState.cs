using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-010 / Q1. JSON wire form is lowercase (matches canonical-resource.schema.json
// `lifecycle` enum). STJ's JsonStringEnumConverter is configured with the
// JsonNamingPolicy.CamelCase policy at the serializer-options layer to emit the lowercase
// form for these flat-named cases.
[JsonConverter(typeof(JsonStringEnumConverter<LifecycleState>))]
public enum LifecycleState
{
    Draft,
    Active,
    Deprecated,
    Retired,
    Archived,
}
