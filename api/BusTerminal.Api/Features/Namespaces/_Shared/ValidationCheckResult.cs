namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §1.2 ValidationCheckResult +
// contracts/validation-run.schema.json#CheckResult. Per-check outcome record
// embedded in ValidationRun.checkResults[]. `Reason` is human-readable but
// strictly categorical (no PII, no raw exception text) per FR-035;
// `ReasonCategory` is the machine-readable enum companion for downstream
// dashboards.
public sealed record ValidationCheckResult(
    ValidationCheckName Name,
    ValidationCheckOutcome Outcome,
    string Reason,
    ValidationFailureCategory ReasonCategory,
    int DurationMs,
    string? CorrelationRequestId = null);
