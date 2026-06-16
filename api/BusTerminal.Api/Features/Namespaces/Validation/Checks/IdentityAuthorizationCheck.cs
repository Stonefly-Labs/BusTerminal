using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Validation.Checks;

// Spec 008 / T074 + FR-014 IdentityAuthorization. Explicit token-acquisition
// probe — verifies the workload UAMI can mint an ARM-scoped token via
// DefaultAzureCredential. Pass on success; Fail with `TokenExchangeFailed`
// when token acquisition raises. Decoupled from `Accessibility` so a
// federation outage surfaces with a distinct diagnostic instead of being
// masked as a generic ARM-call failure.
public sealed class IdentityAuthorizationCheck : INamespaceValidationCheck
{
    private readonly IArmNamespaceProbe _probe;

    public IdentityAuthorizationCheck(IArmNamespaceProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ValidationCheckName Name => ValidationCheckName.IdentityAuthorization;

    public Task<CheckExecutionResult> ExecuteAsync(
        NamespaceArmId armId,
        CancellationToken cancellationToken)
        => CheckExecutor.RunAsync(
            Name,
            ct => _probe.ProbeIdentityAuthorizationAsync(armId, ct),
            cancellationToken);
}
