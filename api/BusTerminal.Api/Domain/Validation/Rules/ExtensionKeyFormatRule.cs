namespace BusTerminal.Api.Domain.Validation.Rules;

// Spec 004 / FR-012 / T131. Defense-in-depth re-run of Extensions' namespaced-key
// pattern at validation time. The Extensions constructor already rejects malformed
// keys, but a future ingest path (sync worker importing from an upstream system)
// could write documents via raw deserialization that bypasses the constructor —
// this rule catches that. Severity is Warning, not Error: we tolerate but flag
// non-standard keys so soft-delete + restore of legacy documents is never blocked.
// The reserved `__indexable` sibling is ignored by the rule.
public sealed class ExtensionKeyFormatRule : IValidationRule
{
    public const string RuleId = "extensions.keyFormat";

    private readonly TimeProvider _time;

    public ExtensionKeyFormatRule(TimeProvider time)
    {
        _time = time;
    }

    public IEnumerable<ValidationFinding> Validate(Resource resource, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);

        foreach (var key in resource.Extensions.Keys)
        {
            if (key == Extensions.IndexableMarkerKey)
            {
                continue;
            }

            if (!Extensions.IsValidKey(key))
            {
                yield return new ValidationFinding(
                    RuleId: RuleId,
                    Severity: ValidationSeverity.Warning,
                    Message: $"Extension key '{key}' is not namespaced as <vendor>:<name>. Legacy documents are tolerated; new writes should use the namespaced form.",
                    EvaluatedAt: _time.GetUtcNow(),
                    FieldRef: $"/extensions/{key}");
            }
        }
    }
}
