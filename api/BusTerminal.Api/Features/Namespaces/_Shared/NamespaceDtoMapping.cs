using BusTerminal.Api.Features.Registry.Shared;

namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / T027. Entity ↔ DTO converters matching the OpenAPI schemas in
// contracts/namespace-onboarding-api.yaml. Per-endpoint request/response
// shapes are introduced by the consuming slices (Onboarding/, Metadata/,
// etc.); this class is the stable seam between the persisted
// `RegistryNamespace` runtime type and the response DTOs that flow back to
// the wizard / inventory / details surfaces.
//
// Per the schema (onboarded-namespace.schema.json), the response shape is the
// full namespace document with all spec-008 fields. We materialize a
// `NamespaceResponse` projection so the API output is decoupled from the
// internal RegistryNamespace shape (allowing internal renames without
// breaking the OpenAPI contract).
public sealed class NamespaceDtoMapping
{
    public NamespaceResponse ToResponse(RegistryNamespace entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new NamespaceResponse(
            Id: entity.Id,
            EntityType: "Namespace",
            Source: entity.Source.ToString(),
            Name: entity.Name,
            FullyQualifiedName: entity.FullyQualifiedName,
            DisplayName: entity.DisplayName,
            Description: entity.Description,
            Environment: entity.Environment,
            Status: entity.Status.ToString(),
            AzureResourceId: entity.AzureResourceId,
            SubscriptionId: entity.SubscriptionId,
            SubscriptionName: entity.SubscriptionName,
            ResourceGroup: entity.ResourceGroup,
            TenantId: entity.TenantId,
            Region: entity.Region,
            BusinessUnit: entity.BusinessUnit,
            ProductOrApplication: entity.ProductOrApplication,
            CostCenter: entity.CostCenter,
            Notes: entity.Notes,
            Tags: entity.Tags,
            LifecycleStatus: entity.LifecycleStatus?.ToString(),
            ValidationStatus: entity.ValidationStatus?.ToString(),
            LastValidationRunId: entity.LastValidationRunId,
            LastValidatedAtUtc: entity.LastValidatedAtUtc,
            Ownership: entity.Ownership,
            OnboardingActor: entity.OnboardingActor,
            CreatedAtUtc: entity.CreatedAtUtc,
            UpdatedAtUtc: entity.UpdatedAtUtc,
            Etag: entity.Etag);
    }
}

// Spec 008 / contracts/onboarded-namespace.schema.json + namespace-onboarding-api.yaml#/OnboardedNamespace.
// External response projection; internal-only renames don't propagate.
public sealed record NamespaceResponse(
    Guid Id,
    string EntityType,
    string Source,
    string Name,
    string? FullyQualifiedName,
    string? DisplayName,
    string? Description,
    string Environment,
    string Status,
    string? AzureResourceId,
    Guid? SubscriptionId,
    string? SubscriptionName,
    string? ResourceGroup,
    Guid? TenantId,
    string? Region,
    string? BusinessUnit,
    string? ProductOrApplication,
    string? CostCenter,
    string? Notes,
    IReadOnlyList<RegistryTag> Tags,
    string? LifecycleStatus,
    string? ValidationStatus,
    Guid? LastValidationRunId,
    DateTimeOffset? LastValidatedAtUtc,
    OwnershipBlock? Ownership,
    OnboardingActor? OnboardingActor,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? Etag);
