using System.Text.RegularExpressions;

namespace BusTerminal.Api.Domain.Validation.Rules;

// Spec 004 / FR-013 + FR-022 / T075. Defense-in-depth re-run of the
// ResourceName regex at validation time. ResourceName's constructor already
// enforces the pattern, but a future ingest path (sync worker importing from
// an upstream system) might construct documents via raw deserialization that
// bypasses the constructor — this rule catches that.
public sealed partial class NamingStandardsRule : IValidationRule
{
    public const string RuleId = "naming.standards";

    private static readonly Regex NamePattern = ValidNameRegex();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidNameRegex();

    private readonly TimeProvider _time;

    public NamingStandardsRule(TimeProvider time)
    {
        _time = time;
    }

    public IEnumerable<ValidationFinding> Validate(Resource resource, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (!string.IsNullOrEmpty(resource.Name.Value) && !NamePattern.IsMatch(resource.Name.Value))
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Resource.Name '{resource.Name.Value}' must match {NamePattern} (lowercase, hyphen-separated, no spaces).",
                EvaluatedAt: _time.GetUtcNow(),
                FieldRef: "/name");
        }
    }
}
