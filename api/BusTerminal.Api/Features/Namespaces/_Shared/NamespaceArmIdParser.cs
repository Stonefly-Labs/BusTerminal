using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / research §10 + data-model.md §5 OnboardingRequest +
// FR-006 (cross-tenant rejection). Canonical ARM id parser that ALSO verifies
// the ARM subscription belongs to the configured Entra tenant. Single subscription→tenant
// lookup per parser instance per subscription id (cached for the lifetime of
// the wizard session — see research §10).
//
// Frontend may pre-validate cross-tenant via the user's MSAL `tid` claim as a
// UX hint, but the authoritative rejection is here (per FR-006: "MUST be
// server-validated").
public sealed partial class NamespaceArmIdParser
{
    // Pattern from contracts/onboarded-namespace.schema.json `azureResourceId`:
    //   /subscriptions/{subId-guid}/resourceGroups/{rg}/providers/Microsoft.ServiceBus/namespaces/{ns}
    // The namespace name follows Service Bus's published rules (6–50 chars,
    // start with letter, end with letter/digit). The resource group accepts
    // any non-slash 1..90 chars per Azure RBAC scope conventions.
    private static readonly Regex ArmIdPattern = BuildPattern();

    [GeneratedRegex(
        @"^/subscriptions/(?<sub>[0-9a-fA-F-]{36})/resourceGroups/(?<rg>[^/]{1,90})/providers/Microsoft\.ServiceBus/namespaces/(?<ns>[A-Za-z][A-Za-z0-9-]{4,48}[A-Za-z0-9])$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex BuildPattern();

    // Distinct pattern with the same prefix shape but a different provider —
    // used to surface a more actionable "wrong resource type" message when an
    // operator pastes an EventHub or Relay ARM id (research §10 / T024 cases).
    private static readonly Regex WrongResourceTypePattern = BuildWrongResourceTypePattern();

    [GeneratedRegex(
        @"^/subscriptions/[0-9a-fA-F-]{36}/resourceGroups/[^/]{1,90}/providers/Microsoft\.(?<provider>EventHub|Relay|NotificationHubs)/",
        RegexOptions.None,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex BuildWrongResourceTypePattern();

    private readonly IArmSubscriptionTenantResolver _resolver;
    private readonly Guid _configuredTenantId;
    private readonly ConcurrentDictionary<Guid, Guid> _tenantCache = new();

    public NamespaceArmIdParser(IArmSubscriptionTenantResolver resolver, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(configuration);
        _resolver = resolver;
        var raw = configuration["AzureAd:TenantId"];
        if (!Guid.TryParse(raw, out _configuredTenantId))
        {
            throw new InvalidOperationException(
                "AzureAd:TenantId must be configured as a Guid to enforce FR-006 cross-tenant rejection.");
        }
    }

    public async Task<NamespaceArmIdParseResult> ParseAndVerifyAsync(
        string? armResourceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(armResourceId))
        {
            return NamespaceArmIdParseResult.Fail(
                ValidationFailureCategory.Unknown,
                "Azure Resource ID is required.");
        }

        var wrongType = WrongResourceTypePattern.Match(armResourceId);
        if (wrongType.Success)
        {
            return NamespaceArmIdParseResult.Fail(
                ValidationFailureCategory.NotFound,
                $"Expected a Service Bus namespace ARM id; got a {wrongType.Groups["provider"].Value} resource.");
        }

        var match = ArmIdPattern.Match(armResourceId);
        if (!match.Success)
        {
            return NamespaceArmIdParseResult.Fail(
                ValidationFailureCategory.Unknown,
                "Azure Resource ID does not match the canonical Service Bus namespace pattern.");
        }

        var subscriptionId = Guid.Parse(match.Groups["sub"].Value);
        var resourceGroup = match.Groups["rg"].Value;
        var namespaceName = match.Groups["ns"].Value;
        var canonical = $"/subscriptions/{subscriptionId:D}/resourceGroups/{resourceGroup}/providers/Microsoft.ServiceBus/namespaces/{namespaceName}";
        var parsed = new NamespaceArmId(canonical, subscriptionId, resourceGroup, namespaceName);

        Guid resolvedTenant;
        if (_tenantCache.TryGetValue(subscriptionId, out var cached))
        {
            resolvedTenant = cached;
        }
        else
        {
            var resolution = await _resolver
                .ResolveTenantIdAsync(subscriptionId, cancellationToken)
                .ConfigureAwait(false);

            switch (resolution.Outcome)
            {
                case TenantResolutionOutcome.Resolved when resolution.TenantId.HasValue:
                    resolvedTenant = resolution.TenantId.Value;
                    _tenantCache[subscriptionId] = resolvedTenant;
                    break;
                case TenantResolutionOutcome.Throttled:
                    return NamespaceArmIdParseResult.Fail(
                        ValidationFailureCategory.Throttled,
                        "ARM throttled the subscription tenant lookup.");
                case TenantResolutionOutcome.Unauthorized:
                    return NamespaceArmIdParseResult.Fail(
                        ValidationFailureCategory.Unauthorized,
                        "BusTerminal cannot read the subscription metadata to verify tenancy.");
                case TenantResolutionOutcome.SubscriptionNotFound:
                    return NamespaceArmIdParseResult.Fail(
                        ValidationFailureCategory.NotFound,
                        "Subscription does not exist or is not visible to BusTerminal.");
                default:
                    return NamespaceArmIdParseResult.Fail(
                        ValidationFailureCategory.Unknown,
                        resolution.Reason ?? "Subscription tenant lookup failed.");
            }
        }

        if (resolvedTenant != _configuredTenantId)
        {
            return NamespaceArmIdParseResult.Fail(
                ValidationFailureCategory.CrossTenant,
                "Azure subscription belongs to a different Entra tenant.",
                parsed with { });
        }

        return NamespaceArmIdParseResult.Ok(parsed with { }, resolvedTenant);
    }
}

public sealed record NamespaceArmIdParseResult(
    NamespaceArmId? ArmId,
    Guid? TenantId,
    ValidationFailureCategory FailureCategory,
    string Reason)
{
    public bool IsSuccess => FailureCategory == ValidationFailureCategory.Ok;

    public static NamespaceArmIdParseResult Ok(NamespaceArmId armId, Guid tenantId)
        => new(armId, tenantId, ValidationFailureCategory.Ok, "OK");

    public static NamespaceArmIdParseResult Fail(
        ValidationFailureCategory category,
        string reason,
        NamespaceArmId? armId = null)
        => new(armId, TenantId: null, category, reason);
}
