using BusTerminal.Api.Domain.Relationships;

namespace BusTerminal.Api.Domain.Validation;

// Spec 004 / T153. Extracted from ValidationEngine so a metered decorator
// (MeteredValidationEngine in Infrastructure/Observability/) can wrap the
// implementation without coupling validation logic to the OTel Meter API.
//
// The concrete ValidationEngine remains the canonical implementation and stays
// registered in DI for direct construction in unit tests; production code
// consumes IValidationEngine so the decorator's metric emission is universal.
public interface IValidationEngine
{
    Task<ValidationResult> ValidateAsync(
        Resource resource,
        Func<ResourceId, Resource?> relationshipResolver,
        Func<Resource, bool> duplicateDetector,
        LifecycleState? previousLifecycle = null,
        CancellationToken cancellationToken = default);

    Task<ValidationResult> ValidateRelationshipAsync(
        Relationship relationship,
        Func<ResourceId, Resource?> relationshipResolver,
        Func<Resource, bool> duplicateDetector,
        CancellationToken cancellationToken = default);
}

// Meter name constants per data-model.md § Naming Cross-Reference.
// `BusTerminal.Validation` is the OTel Meter; the three Counter instruments
// landed beneath it are the metric names the table mandates.
public static class ValidationMeter
{
    public const string Name = "BusTerminal.Validation";

    public const string FindingCountError = "busterminal.validation.finding_count_error";
    public const string FindingCountWarning = "busterminal.validation.finding_count_warning";
    public const string FindingCountInfo = "busterminal.validation.finding_count_info";

    // Tag key emitted alongside every counter increment so operators can pivot
    // by resource type. Tag VALUE is the discriminator (e.g., "queue", "topic")
    // — a controlled-vocabulary string with no PII (constitution §V "no PII in
    // telemetry by default" preserved).
    public const string ResourceTypeTag = "busterminal.resource.type";
}
