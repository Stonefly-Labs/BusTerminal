namespace BusTerminal.Api.Domain.Validation.Rules;

// Spec 004 / FR-013 / T074. Required base fields per data-model.md §1.
// `Id`, `ResourceType`, `Name`, `DisplayName`, `Lifecycle`, `Version`, `Audit`
// are all required — the C# `required` modifier already enforces this at
// construction time, but the rule re-asserts it at validation time so a
// deserialized-from-broken-payload document with missing fields still surfaces
// a structured finding instead of failing only at serialization.
public sealed class RequiredFieldsRule : IValidationRule
{
    public const string RuleId = "required.fields";

    private readonly TimeProvider _time;

    public RequiredFieldsRule(TimeProvider time)
    {
        _time = time;
    }

    public IEnumerable<ValidationFinding> Validate(Resource resource, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var now = _time.GetUtcNow();

        if (resource.Id == default)
        {
            yield return Finding(now, "/id", "Resource.Id is required.");
        }

        if (string.IsNullOrEmpty(resource.ResourceType))
        {
            yield return Finding(now, "/resourceType", "Resource.ResourceType is required.");
        }

        if (string.IsNullOrEmpty(resource.Name.Value))
        {
            yield return Finding(now, "/name", "Resource.Name is required.");
        }

        if (string.IsNullOrWhiteSpace(resource.DisplayName))
        {
            yield return Finding(now, "/displayName", "Resource.DisplayName is required.");
        }

        if (resource.Version is null)
        {
            yield return Finding(now, "/version", "Resource.Version is required.");
        }

        if (resource.Audit is null)
        {
            yield return Finding(now, "/audit", "Resource.Audit is required.");
        }
    }

    private static ValidationFinding Finding(DateTimeOffset now, string fieldRef, string message) =>
        new(RuleId, ValidationSeverity.Error, message, now, fieldRef);
}
