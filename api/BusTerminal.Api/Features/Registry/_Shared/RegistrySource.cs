using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-004. Server-stamped provenance.
// `Discovered` is reserved for a future automatic-discovery spec per
// data-model.md §10 — until then only `Manual` is legal on the wire.
[JsonConverter(typeof(JsonStringEnumConverter<RegistrySource>))]
public enum RegistrySource
{
    Manual,
}
