namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §1.2 DriftField. One entry per Azure-identifier
// field that differs between the persisted namespace document and the
// ARM-observed value at run time. `field` is constrained to
// {"region", "resourceGroup", "subscriptionId"} by the validation runner.
public sealed record DriftField(
    string Field,
    string PersistedValue,
    string ObservedValue);
