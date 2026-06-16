using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;

namespace BusTerminal.Api.Features.Namespaces.Onboarding;

// Spec 008 / data-model.md §5 OnboardingRequest +
// contracts/namespace-onboarding-api.yaml#/RegisterNamespaceRequest.
// Wizard's step-5 register payload. The pre-allocated `id` is the namespace
// Guid the wizard stamped at the start of step 4 (research §18); the
// `validationRunId` MUST reference a run with `namespaceId == id`.
public sealed record OnboardingRequest(
    Guid Id,
    string AzureResourceId,
    string DisplayName,
    string Environment,
    string? Description,
    string? BusinessUnit,
    string? ProductOrApplication,
    string? CostCenter,
    string? Notes,
    IReadOnlyList<RegistryTag>? Tags,
    OwnershipBlock Ownership,
    Guid ValidationRunId);
