using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Validation.Checks;

// Spec 008 / T075 + FR-014 ApiReachability + research §14. Lightweight
// `GET https://{namespaceName}.servicebus.windows.net/$Resources?api-version=2017-04`
// against the Service Bus management endpoint. 200/401/403 all Pass — the
// management endpoint is reachable; auth is the IdentityAuthorization
// check's job. Only network-level failures (DNS, TLS, timeout) Fail with
// `ServiceBusManagementUnreachable`.
public sealed class ApiReachabilityCheck : INamespaceValidationCheck
{
    private readonly IArmNamespaceProbe _probe;

    public ApiReachabilityCheck(IArmNamespaceProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ValidationCheckName Name => ValidationCheckName.ApiReachability;

    public Task<CheckExecutionResult> ExecuteAsync(
        NamespaceArmId armId,
        CancellationToken cancellationToken)
        => CheckExecutor.RunAsync(
            Name,
            ct => _probe.ProbeApiReachabilityAsync(armId, ct),
            cancellationToken);
}
