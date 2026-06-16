namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §1.2 + contracts/validation-run.schema.json.
// Append-only record of one validation execution. Persisted in the new
// `namespace-validation-runs` Cosmos container (PK `/namespaceId`).
//
// `NamespaceId` allocation (research §18): for wizard-driven pre-onboarding
// runs the frontend pre-allocates the namespace Guid at the start of step 4
// and re-uses it as both the ValidationRun partition key and the eventual
// OnboardedNamespace.id — keeping pre-onboarding runs partition-aligned with
// the registered namespace from day one. Direct API callers MAY omit
// `proposedNamespaceId` on POST /api/namespaces/_validate; the runner
// generates a fresh Guid in that case (run is not bindable to a future
// namespace document).
public sealed record ValidationRun(
    Guid Id,
    Guid NamespaceId,
    DateTimeOffset ExecutedAtUtc,
    Guid ExecutedBy,
    string ExecutedByDisplayNameSnapshot,
    string AzureResourceIdAtRun,
    ValidationStatus AggregateStatus,
    IReadOnlyList<ValidationCheckResult> CheckResults,
    bool DriftDetected,
    IReadOnlyList<DriftField> DriftFields,
    int TotalDurationMs,
    ArmResourceSnapshot? ArmResourceSnapshot = null,
    string? Etag = null);
