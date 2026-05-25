namespace BusTerminal.Api.Domain.Validation.Rules;

// Spec 004 / FR-010 + FR-013 + Q1 / T120. Fires when an Update changes the
// resource's Lifecycle and the from/to pair is not in the legal transition map
// from contracts/lifecycle-transitions.md.
//
// The "intended transition" is carried via ValidationContext.PreviousLifecycle:
// the caller (a CLI verb today; the API write surface tomorrow) reads the
// existing resource from the store and passes its Lifecycle to ValidateAsync.
// On Create the carrier is null and the rule yields nothing.
//
// Severity: Error — illegal transitions block the write. Soft-delete + restore
// are NOT lifecycle transitions per contracts/lifecycle-transitions.md and run
// through dedicated store paths (SoftDeleteAsync / RestoreAsync) that do not
// invoke this rule.
public sealed class LifecycleTransitionRule : IValidationRule
{
    public const string RuleId = "lifecycle.transition";

    private readonly TimeProvider _time;

    public LifecycleTransitionRule(TimeProvider time)
    {
        _time = time;
    }

    public IEnumerable<ValidationFinding> Validate(Resource resource, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(context);

        if (context.PreviousLifecycle is not { } previous)
        {
            yield break;
        }

        var target = resource.Lifecycle;
        if (LifecycleTransitions.IsTransitionLegal(previous, target))
        {
            yield break;
        }

        yield return new ValidationFinding(
            RuleId: RuleId,
            Severity: ValidationSeverity.Error,
            Message: $"Illegal lifecycle transition for resource {resource.Id}: {previous} -> {target}.",
            EvaluatedAt: _time.GetUtcNow(),
            FieldRef: "/lifecycle");
    }
}
