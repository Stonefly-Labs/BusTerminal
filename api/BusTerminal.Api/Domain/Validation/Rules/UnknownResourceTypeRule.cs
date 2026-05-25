using BusTerminal.Api.Domain.Resources;

namespace BusTerminal.Api.Domain.Validation.Rules;

// Spec 004 / Q4 + FR-002 / T076. Emits an Info finding (NOT Error or Warning —
// the document is structurally fine; the build just doesn't know its type yet)
// so operators can quickly surface "we have N stored documents whose type isn't
// in the current build's registry."
public sealed class UnknownResourceTypeRule : IValidationRule
{
    public const string RuleId = "unknown.resourceType";

    private readonly TimeProvider _time;

    public UnknownResourceTypeRule(TimeProvider time)
    {
        _time = time;
    }

    public bool AppliesTo(Type resourceType) => resourceType == typeof(UnknownResource);

    public IEnumerable<ValidationFinding> Validate(Resource resource, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);

        yield return new ValidationFinding(
            RuleId: RuleId,
            Severity: ValidationSeverity.Info,
            Message: $"Resource has an unknown resourceType '{resource.ResourceType}'. Document was materialized as UnknownResource and preserved as-is.",
            EvaluatedAt: _time.GetUtcNow(),
            FieldRef: "/resourceType");
    }
}
