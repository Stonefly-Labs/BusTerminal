namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / research §10. Abstraction over ARM subscription metadata
// resolution so NamespaceArmIdParser can be unit-tested without a real
// ArmClient. The Infrastructure/ServiceBus/ArmSubscriptionTenantResolver
// implementation backs this with the singleton ArmClient.
public interface IArmSubscriptionTenantResolver
{
    Task<TenantResolution> ResolveTenantIdAsync(Guid subscriptionId, CancellationToken cancellationToken);
}

public sealed record TenantResolution(
    Guid? TenantId,
    TenantResolutionOutcome Outcome,
    string? Reason = null);

public enum TenantResolutionOutcome
{
    Resolved,
    SubscriptionNotFound,
    Throttled,
    Unauthorized,
    Failed,
}
