using BusTerminal.Api.Domain.Validation;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-001. The shared shape every first-class entity inherits.
// Per-type derived records add type-specific fields only (no overrides of base fields).
//
// Polymorphic serialization: handled by ResourceJsonConverter (Domain/Serialization/),
// which dispatches over the ResourceTypeRegistry on the `resourceType` discriminator
// and falls through to UnknownResource for unknown values (Q4).
public abstract record Resource
{
    public required ResourceId Id { get; init; }

    public required string ResourceType { get; init; }

    public required ResourceName Name { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required NamespacePath NamespacePath { get; init; }

    public IReadOnlyCollection<EnvironmentClassification> Environments { get; init; } = [];

    public required LifecycleState Lifecycle { get; init; }

    public required SemanticVersion Version { get; init; }

    public OwnershipRecord? Ownership { get; init; }

    public required AuditRecord Audit { get; init; }

    public ClassificationMetadata? Classification { get; init; }

    public IReadOnlyCollection<TagReference> Tags { get; init; } = [];

    public Extensions Extensions { get; init; } = Extensions.Empty;

    public IReadOnlyCollection<DocumentationReference> Documentation { get; init; } = [];

    public ValidationResult? ValidationState { get; init; }

    public ConcurrencyToken ConcurrencyToken { get; init; } = ConcurrencyToken.Empty;

    public bool IsDeleted { get; init; }
}

// Sub-record colocated with Resource so the base shape is one place to read.
// Matches canonical-resource.schema.json `classification`.
public sealed record ClassificationMetadata(
    OperationalTier? Criticality = null,
    string? DataSensitivity = null,
    string? ComplianceScope = null,
    string? AvailabilityTier = null,
    string? BusinessDomain = null,
    OperationalTier? OperationalTier = null);
