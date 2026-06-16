using Azure;
using Azure.Core;
using Azure.ResourceManager;
using BusTerminal.Api.Features.Namespaces.Shared;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Api.Infrastructure.ServiceBus;

// Spec 008 / research §10. Real implementation backing NamespaceArmIdParser's
// tenant verification. Resolves a subscription's owning Entra tenant via
// `ArmClient.GetSubscriptionResource(subId).GetAsync()`. The parser keeps a
// short-lived in-process cache; this resolver is consulted on cache miss.
public sealed partial class ArmSubscriptionTenantResolver : IArmSubscriptionTenantResolver
{
    private readonly ArmClient _armClient;
    private readonly ILogger<ArmSubscriptionTenantResolver> _logger;

    public ArmSubscriptionTenantResolver(ArmClient armClient, ILogger<ArmSubscriptionTenantResolver> logger)
    {
        _armClient = armClient;
        _logger = logger;
    }

    [LoggerMessage(EventId = 8501, Level = LogLevel.Warning, Message = "ARM subscription tenant resolution failed for {SubscriptionId} (status {Status})")]
    private partial void LogResolutionFailure(Guid subscriptionId, int status);

    public async Task<TenantResolution> ResolveTenantIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscriptionResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId:D}");
        var subscription = _armClient.GetSubscriptionResource(subscriptionResourceId);

        try
        {
            var response = await subscription.GetAsync(cancellationToken).ConfigureAwait(false);
            var tenantId = response.Value.Data.TenantId;
            return tenantId.HasValue
                ? new TenantResolution(tenantId.Value, TenantResolutionOutcome.Resolved)
                : new TenantResolution(null, TenantResolutionOutcome.Failed,
                    "Subscription metadata did not include a tenantId.");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            LogResolutionFailure(subscriptionId, ex.Status);
            return new TenantResolution(null, TenantResolutionOutcome.SubscriptionNotFound, "404 from ARM");
        }
        catch (RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            LogResolutionFailure(subscriptionId, ex.Status);
            return new TenantResolution(null, TenantResolutionOutcome.Unauthorized, $"{ex.Status} from ARM");
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            LogResolutionFailure(subscriptionId, ex.Status);
            return new TenantResolution(null, TenantResolutionOutcome.Throttled, "429 from ARM");
        }
        catch (RequestFailedException ex)
        {
            LogResolutionFailure(subscriptionId, ex.Status);
            return new TenantResolution(null, TenantResolutionOutcome.Failed, $"ARM status {ex.Status}");
        }
    }
}
