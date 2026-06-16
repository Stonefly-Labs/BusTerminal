using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Validation.Checks;

// Spec 008 / research §5. Common port that the runner orchestrates via
// Task.WhenAll. Each implementation wraps the corresponding IArmNamespaceProbe
// surface, owns its child OTel span, and returns a uniform check result that
// the runner aggregates into the persisted ValidationRun.
//
// The runner is the only ActivitySource consumer for the check span (per the
// ArmNamespaceProbe comment header). Probes stay span-free so adapter
// implementations stay loose-coupled to telemetry concerns.
public interface INamespaceValidationCheck
{
    ValidationCheckName Name { get; }

    Task<CheckExecutionResult> ExecuteAsync(NamespaceArmId armId, CancellationToken cancellationToken);
}
