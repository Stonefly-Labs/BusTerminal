using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Validation.Checks;

// Spec 008 / T072 + FR-014 Accessibility. Same underlying ARM call as
// Existence — but evaluates the *auth/response* shape only, not resource
// presence. Pass when ARM responds without an auth failure (including 404 —
// the management plane is reachable; the namespace just doesn't exist
// there, which is Existence's concern). Fail when ARM returns 401/403,
// 429 (Throttled), or the call times out.
public sealed class AccessibilityCheck : INamespaceValidationCheck
{
    private readonly IArmNamespaceProbe _probe;

    public AccessibilityCheck(IArmNamespaceProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ValidationCheckName Name => ValidationCheckName.Accessibility;

    public Task<CheckExecutionResult> ExecuteAsync(
        NamespaceArmId armId,
        CancellationToken cancellationToken)
        => CheckExecutor.RunAsync(
            Name,
            ct => _probe.ProbeAccessibilityAsync(armId, ct),
            cancellationToken);
}
