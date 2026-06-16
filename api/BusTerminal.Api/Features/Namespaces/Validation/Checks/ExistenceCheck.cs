using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Validation.Checks;

// Spec 008 / T071 + FR-014 Existence. ARM `GET` against the namespace
// resource. Pass when the ARM SDK returns the ServiceBusNamespaceResource;
// the probe maps 404 → NotFound, 401/403 → Unauthorized, 429 → Throttled,
// timeout → Timeout, success → Pass. The probe also captures the
// ArmResourceSnapshot here so the runner can drift-detect against the
// persisted document later (research §11).
public sealed class ExistenceCheck : INamespaceValidationCheck
{
    private readonly IArmNamespaceProbe _probe;

    public ExistenceCheck(IArmNamespaceProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ValidationCheckName Name => ValidationCheckName.Existence;

    public Task<CheckExecutionResult> ExecuteAsync(
        NamespaceArmId armId,
        CancellationToken cancellationToken)
        => CheckExecutor.RunAsync(
            Name,
            ct => _probe.ProbeExistenceAsync(armId, ct),
            cancellationToken);
}
