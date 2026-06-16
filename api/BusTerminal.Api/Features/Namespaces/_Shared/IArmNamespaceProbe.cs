namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / research §1, §3, §14. Adapter port for Azure.ResourceManager.ServiceBus
// + ARM `permissions/list` + Service Bus management endpoint probe. One typed
// surface per validation check (Existence, Accessibility, RequiredPermissions,
// IdentityAuthorization, ApiReachability) so the runner reads each result
// without inspecting raw ARM SDK responses. Implementation lives in
// Infrastructure/ServiceBus/ArmNamespaceProbe.cs.
public interface IArmNamespaceProbe
{
    Task<ArmProbeResult> ProbeExistenceAsync(NamespaceArmId armId, CancellationToken cancellationToken);

    Task<ArmProbeResult> ProbeAccessibilityAsync(NamespaceArmId armId, CancellationToken cancellationToken);

    Task<ArmProbeResult> ProbeRequiredPermissionsAsync(NamespaceArmId armId, CancellationToken cancellationToken);

    Task<ArmProbeResult> ProbeIdentityAuthorizationAsync(NamespaceArmId armId, CancellationToken cancellationToken);

    Task<ArmProbeResult> ProbeApiReachabilityAsync(NamespaceArmId armId, CancellationToken cancellationToken);
}

// Spec 008 / data-model.md §6 "Reason categories". The probe never emits raw
// exception text — categorical reasons only. Each check method returns this
// shape so the runner builds a uniform ValidationCheckResult per FR-035.
public sealed record ArmProbeResult(
    ValidationCheckOutcome Outcome,
    ValidationFailureCategory ReasonCategory,
    string Reason,
    string? CorrelationRequestId = null,
    ArmResourceSnapshot? Snapshot = null);
