namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §1.1 + research §10. Parsed and canonicalized
// ARM Service Bus namespace identifier. Created by NamespaceArmIdParser
// after format validation and cross-tenant verification have BOTH passed.
public sealed record NamespaceArmId(
    string CanonicalArmId,
    Guid SubscriptionId,
    string ResourceGroup,
    string NamespaceName);
