using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §2 LifecycleStatus. Operational axis (orthogonal
// to spec 006's `RegistryEntityStatus` governance axis). `PendingValidation`
// is transient-only and NEVER appears on a persisted document per FR-022 —
// reserved for a future async-validation spec.
//
// Permitted transitions (FR-023):
//   - (initial create) → Active
//   - Active ⇄ Disabled
//   - Active | Disabled → Archived
//   - Archived → Disabled
[JsonConverter(typeof(JsonStringEnumConverter<LifecycleStatus>))]
public enum LifecycleStatus
{
    PendingValidation,
    Active,
    Disabled,
    Archived,
}
