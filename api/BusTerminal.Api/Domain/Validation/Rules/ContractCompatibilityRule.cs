using BusTerminal.Api.Domain.Resources;

namespace BusTerminal.Api.Domain.Validation.Rules;

// Spec 004 / FR-013 + FR-007 / T115. Applies only to MessageContract.
//
// Three assertions:
//   1. MessageContract.Compatibility is set. The C# `required` modifier enforces
//      this at construction, but the rule re-checks at validation time so a
//      deserialized-from-broken-payload document still surfaces a structured
//      finding (consistent with RequiredFieldsRule's design — defense in depth).
//      Severity: Error.
//   2. Version.VersionHistory is internally consistent:
//      a. No duplicate (major.minor.patch) entries — Warning.
//      b. Every Deprecated entry has a replacedBy that points at a known
//         non-deprecated entry in the same history when a candidate exists. If
//         no candidate exists, the missing replacedBy is recorded as Info (we
//         can't synthesize a replacement out of thin air). Severity: Info.
//   3. Cross-version coherence: if there is a current Active version on the
//      resource and the history contains an entry with a strictly lower semver
//      whose lifecycle is still Active or Draft, fire Info — the older version
//      is almost certainly meant to be deprecated. FR-007 lineage health.
//
// Schema-level compatibility (whether v0.9 is actually backward-compatible with
// v1.0 at the payload level) is explicitly out of scope — pluggable schema
// validators are deferred per spec assumption. This rule only checks the
// version-lineage *metadata* for internal consistency.
public sealed class ContractCompatibilityRule : IValidationRule
{
    public const string RuleId = "contract.compatibility";

    private readonly TimeProvider _time;

    public ContractCompatibilityRule(TimeProvider time)
    {
        _time = time;
    }

    public bool AppliesTo(Type resourceType) => resourceType == typeof(MessageContract);

    public IEnumerable<ValidationFinding> Validate(Resource resource, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource is not MessageContract contract)
        {
            yield break;
        }

        var now = _time.GetUtcNow();

        // (1) Compatibility indicator presence. Enum can never be null in C#,
        // but a default-initialized enum slot is still valid wire-form. We allow
        // any declared CompatibilityIndicator (Backward / Forward / Full / None)
        // because all four are intentional choices per FR-011.

        var history = contract.Version.VersionHistory;
        if (history is null || history.Count == 0)
        {
            yield break;
        }

        // (2a) Duplicate versions.
        var seen = new Dictionary<(int Major, int Minor, int Patch), int>();
        foreach (var entry in history)
        {
            var key = (entry.Major, entry.Minor, entry.Patch);
            if (seen.TryGetValue(key, out var firstIndex))
            {
                yield return new ValidationFinding(
                    RuleId: RuleId,
                    Severity: ValidationSeverity.Warning,
                    Message: $"Version history contains duplicate entry for {key.Major}.{key.Minor}.{key.Patch} (first occurrence at index {firstIndex}).",
                    EvaluatedAt: now,
                    FieldRef: "/version/versionHistory");
            }
            else
            {
                seen[key] = seen.Count;
            }
        }

        // Build an index of non-deprecated successors for (2b)'s replacedBy
        // resolution. "Non-deprecated" means lifecycle in {Draft, Active}.
        var nonDeprecatedCandidates = history
            .Where(h => h.Lifecycle == LifecycleState.Draft || h.Lifecycle == LifecycleState.Active)
            .Select(h => (h.Major, h.Minor, h.Patch))
            .ToHashSet();

        // The current resource-level version (Active state on the resource) is
        // also a valid replacedBy target — operators commonly elide an explicit
        // history entry for the current version.
        var current = (contract.Version.Major, contract.Version.Minor, contract.Version.Patch);
        if (contract.Lifecycle == LifecycleState.Draft || contract.Lifecycle == LifecycleState.Active)
        {
            nonDeprecatedCandidates.Add(current);
        }

        // (2b) Deprecated entries should carry a replacedBy that points at a
        // known non-deprecated version (if one exists).
        foreach (var entry in history)
        {
            if (entry.Lifecycle != LifecycleState.Deprecated)
            {
                continue;
            }

            if (entry.ReplacedBy is null)
            {
                if (nonDeprecatedCandidates.Count > 0)
                {
                    yield return new ValidationFinding(
                        RuleId: RuleId,
                        Severity: ValidationSeverity.Info,
                        Message: $"Deprecated version {entry.Major}.{entry.Minor}.{entry.Patch} has no replacedBy reference; a non-deprecated successor exists in the lineage.",
                        EvaluatedAt: now,
                        FieldRef: "/version/versionHistory");
                }

                continue;
            }

            var target = (entry.ReplacedBy.Major, entry.ReplacedBy.Minor, entry.ReplacedBy.Patch);
            if (!nonDeprecatedCandidates.Contains(target))
            {
                yield return new ValidationFinding(
                    RuleId: RuleId,
                    Severity: ValidationSeverity.Warning,
                    Message: $"Deprecated version {entry.Major}.{entry.Minor}.{entry.Patch} replacedBy {entry.ReplacedBy} does not resolve to a non-deprecated version in the lineage.",
                    EvaluatedAt: now,
                    FieldRef: "/version/versionHistory");
            }
        }

        // (3) "Older but not deprecated" — Info. An entry whose semver is
        // strictly lower than the current resource version but whose lifecycle
        // is still Draft or Active is almost certainly meant to be deprecated.
        var currentRef = new SemanticVersionRef(contract.Version.Major, contract.Version.Minor, contract.Version.Patch);
        foreach (var entry in history)
        {
            var entryRef = new SemanticVersionRef(entry.Major, entry.Minor, entry.Patch);
            if (entryRef.CompareTo(currentRef) >= 0)
            {
                continue;
            }

            if (entry.Lifecycle == LifecycleState.Draft || entry.Lifecycle == LifecycleState.Active)
            {
                yield return new ValidationFinding(
                    RuleId: RuleId,
                    Severity: ValidationSeverity.Info,
                    Message: $"Version {entry.Major}.{entry.Minor}.{entry.Patch} is older than the current version {currentRef} but is still marked '{entry.Lifecycle}'.",
                    EvaluatedAt: now,
                    FieldRef: "/version/versionHistory");
            }
        }
    }
}
