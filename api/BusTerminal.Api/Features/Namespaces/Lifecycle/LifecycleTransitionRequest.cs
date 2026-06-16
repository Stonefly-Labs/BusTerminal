using System.Text.Json;
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

// Wire shape is camelCase (`disable | enable | archive | restore`) per the
// OpenAPI contract — the converter below applies that naming policy at
// deserialization + serialization.
[JsonConverter(typeof(LifecycleActionJsonConverter))]
public enum LifecycleAction
{
    Disable,
    Enable,
    Archive,
    Restore,
}

internal sealed class LifecycleActionJsonConverter : JsonStringEnumConverter<LifecycleAction>
{
    public LifecycleActionJsonConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    {
    }
}
