using System.Diagnostics;
using BusTerminal.Api.Features.Namespaces.Shared;

namespace BusTerminal.Api.Features.Namespaces.Validation;

// Spec 008 / research §5 + plan.md §Principle V. Singleton ActivitySource for
// the namespace-onboarding slice. The runner emits four span trees per the
// plan:
//   - namespace.onboarding.run (wraps wizard step-5 register end-to-end)
//   - namespace.validation.rerun (wraps standalone re-runs from the details page)
//   - namespace.lifecycle.transition (per transition)
//   - namespace.metadata.update / namespace.ownership.update (per write)
// Each parent span has child spans namespace.validation.check.<name> with
// validation.check.outcome / .duration_ms / .reason_category attributes.
//
// PII boundary: ownership block contents (display names, object ids) are
// NEVER added to spans — only correlation identifiers per FR-035.
public static class NamespaceValidationActivitySource
{
    public const string Name = "BusTerminal.NamespaceOnboarding";

    public static readonly ActivitySource Source = new(Name);

    public const string OnboardingRunSpan = "namespace.onboarding.run";
    public const string ValidationRerunSpan = "namespace.validation.rerun";
    public const string LifecycleTransitionSpan = "namespace.lifecycle.transition";
    public const string MetadataUpdateSpan = "namespace.metadata.update";
    public const string OwnershipUpdateSpan = "namespace.ownership.update";

    public const string CheckSpanPrefix = "namespace.validation.check.";

    public static Activity? StartCheckSpan(ValidationCheckName check, Activity? parent = null)
    {
        return Source.StartActivity(
            $"{CheckSpanPrefix}{check}",
            ActivityKind.Internal,
            parent?.Context ?? default);
    }

    public static void SetCheckOutcome(
        this Activity? activity,
        ValidationCheckOutcome outcome,
        ValidationFailureCategory reasonCategory,
        int durationMs,
        string? correlationRequestId = null)
    {
        if (activity is null) return;
        activity.SetTag("validation.check.outcome", outcome.ToString());
        activity.SetTag("validation.check.reason_category", reasonCategory.ToString());
        activity.SetTag("validation.check.duration_ms", durationMs);
        if (!string.IsNullOrEmpty(correlationRequestId))
        {
            activity.SetTag("azure.arm.x_ms_correlation_request_id", correlationRequestId);
        }
    }

    public static void SetAggregateStatus(
        this Activity? activity,
        ValidationStatus status,
        bool driftDetected)
    {
        if (activity is null) return;
        activity.SetTag("validation.aggregate_status", status.ToString());
        activity.SetTag("namespace.validation.run.drift_detected", driftDetected);
    }
}
