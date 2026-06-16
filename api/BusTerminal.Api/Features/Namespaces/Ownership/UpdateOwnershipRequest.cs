using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Ownership;

// Spec 008 / data-model.md §5 UpdateOwnershipRequest +
// contracts/namespace-onboarding-api.yaml#/UpdateOwnershipRequest. Full-block
// replace — partial ownership updates are NOT supported in v1.
public sealed record UpdateOwnershipRequest(
    Guid Id,
    OwnershipBlock Ownership);
