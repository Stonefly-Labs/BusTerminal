namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §1.2 + research §11. Captured from ARM when the
// Existence check passes; null when Existence fails. The runner compares the
// snapshot against the persisted namespace document's Azure-identifier fields
// to populate ValidationRun.driftDetected / driftFields without ever mutating
// the persisted document (FR-029 — drift is surfaced, not auto-reconciled).
public sealed record ArmResourceSnapshot(
    string Region,
    string ResourceGroup,
    Guid SubscriptionId,
    DateTimeOffset CapturedAtUtc);
