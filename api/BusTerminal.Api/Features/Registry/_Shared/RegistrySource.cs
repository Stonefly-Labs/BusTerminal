using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-004 + Spec 008 / research §7. Server-stamped provenance.
// `Manual`: created via the spec-006 polymorphic registry form.
// `Onboarded`: created via the spec-008 namespace onboarding wizard (validation-verified).
// `Discovered` is reserved for a future automatic-discovery spec.
[JsonConverter(typeof(JsonStringEnumConverter<RegistrySource>))]
public enum RegistrySource
{
    Manual,
    Onboarded,
}
