using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §2 ValidationStatus. Mirrors the latest ValidationRun's
// aggregate outcome onto the namespace document. `Healthy` = all five checks Pass;
// `Degraded` = at least one non-fatal Fail with Existence + Accessibility Pass;
// `Unhealthy` = Existence or Accessibility Fail (initial onboarding hard-blocked
// at this status per FR-023a).
[JsonConverter(typeof(JsonStringEnumConverter<ValidationStatus>))]
public enum ValidationStatus
{
    Healthy,
    Degraded,
    Unhealthy,
}
