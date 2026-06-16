using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Validation.Checks;

// Spec 008 / T073 + FR-014 RequiredPermissions + research §3. ARM
// `permissions/list` at the namespace scope. Asserts the workload UAMI's
// effective actions include `Microsoft.ServiceBus/namespaces/read` or a
// wildcard that subsumes it. Fail with `ReaderRoleMissing` when not present
// — the wizard step-4 panel renders this as the runbook remediation hint
// pointing at `iac/runbooks/grant-namespace-reader.md` (research §4).
public sealed class RequiredPermissionsCheck : INamespaceValidationCheck
{
    private readonly IArmNamespaceProbe _probe;

    public RequiredPermissionsCheck(IArmNamespaceProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ValidationCheckName Name => ValidationCheckName.RequiredPermissions;

    public Task<CheckExecutionResult> ExecuteAsync(
        NamespaceArmId armId,
        CancellationToken cancellationToken)
        => CheckExecutor.RunAsync(
            Name,
            ct => _probe.ProbeRequiredPermissionsAsync(armId, ct),
            cancellationToken);
}
