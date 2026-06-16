using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Validation.Checks;

// Spec 008 / research §11. Composite return from a check — the runner needs
// both the persisted-shape ValidationCheckResult AND the optional
// ArmResourceSnapshot the Existence probe captured. ValidationCheckResult is
// the only thing that lands in the persisted ValidationRun document; the
// snapshot is a runtime side-channel used by the runner for drift detection.
public sealed record CheckExecutionResult(
    ValidationCheckResult Result,
    ArmResourceSnapshot? Snapshot);
