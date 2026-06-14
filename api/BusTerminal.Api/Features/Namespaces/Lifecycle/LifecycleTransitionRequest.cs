using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Namespaces.Lifecycle;

// Spec 008 / data-model.md §5 LifecycleTransitionRequest +
// contracts/namespace-onboarding-api.yaml. POST body for
// `/api/namespaces/{id}/lifecycle`. `reason` is required for disable / archive /
// restore (FR-023); validators reject when missing for those actions.
public sealed record LifecycleTransitionRequest(
    Guid Id,
    LifecycleAction Action,
    string? Reason);

[JsonConverter(typeof(JsonStringEnumConverter<LifecycleAction>))]
public enum LifecycleAction
{
    Disable,
    Enable,
    Archive,
    Restore,
}
