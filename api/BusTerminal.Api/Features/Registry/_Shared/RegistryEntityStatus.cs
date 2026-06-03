using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-002 / FR-013a. Two-state lifecycle for the registry slice.
// `Deleted` is reserved per data-model.md §10 — emitting it is a contract
// violation. A future soft-delete spec re-introduces the third member.
[JsonConverter(typeof(JsonStringEnumConverter<RegistryEntityStatus>))]
public enum RegistryEntityStatus
{
    Active,
    Deprecated,
}
