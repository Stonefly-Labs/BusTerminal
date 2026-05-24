using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Domain.Validation.Rules;
using BusTerminal.Api.Tests.Unit.Serialization;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Validation.Rules;

// Spec 004 / T118 / FR-007 + FR-013. Locks in the version-lineage health
// semantics for MessageContract: clean lineage produces no findings, duplicate
// version entries surface Warnings, deprecated-without-replacedBy surfaces an
// Info finding, and an older-but-still-Active entry surfaces an Info finding.
public sealed class ContractCompatibilityRuleTests
{
    [Fact]
    public void AppliesTo_returns_true_only_for_MessageContract()
    {
        var rule = new ContractCompatibilityRule(TimeProvider.System);

        rule.AppliesTo(typeof(MessageContract)).Should().BeTrue();
        rule.AppliesTo(typeof(Queue)).Should().BeFalse();
        rule.AppliesTo(typeof(Topic)).Should().BeFalse();
        rule.AppliesTo(typeof(Team)).Should().BeFalse();
    }

    [Fact]
    public void Consistent_lineage_produces_no_findings()
    {
        // v1.0.0 Active + v0.9.0 Deprecated with replacedBy → v1.0.0 (the
        // canonical happy path 03-contracts.json models).
        var contract = ResourceFactory.BuildMessageContract() with
        {
            Version = new SemanticVersion(
                Major: 1,
                Minor: 0,
                Patch: 0,
                Compatibility: CompatibilityIndicator.Backward,
                VersionHistory:
                [
                    new HistoricalVersionEntry(
                        Major: 0,
                        Minor: 9,
                        Patch: 0,
                        Lifecycle: LifecycleState.Deprecated,
                        DeprecatedAt: DateTimeOffset.Parse("2025-10-01T00:00:00Z"),
                        ReplacedBy: new SemanticVersionRef(1, 0, 0)),
                ]),
        };

        var rule = new ContractCompatibilityRule(TimeProvider.System);
        var findings = rule.Validate(contract, BuildContext()).ToList();

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Empty_or_missing_version_history_produces_no_findings()
    {
        var contract = ResourceFactory.BuildMessageContract();

        var rule = new ContractCompatibilityRule(TimeProvider.System);
        var findings = rule.Validate(contract, BuildContext()).ToList();

        findings.Should().BeEmpty(
            "a contract with a single live version and no recorded predecessors has nothing to validate");
    }

    [Fact]
    public void Duplicate_version_entry_fires_warning()
    {
        var contract = ResourceFactory.BuildMessageContract() with
        {
            Version = new SemanticVersion(
                Major: 1,
                Minor: 0,
                Patch: 0,
                Compatibility: CompatibilityIndicator.Backward,
                VersionHistory:
                [
                    new HistoricalVersionEntry(0, 9, 0, LifecycleState.Deprecated, ReplacedBy: new SemanticVersionRef(1, 0, 0)),
                    new HistoricalVersionEntry(0, 9, 0, LifecycleState.Deprecated, ReplacedBy: new SemanticVersionRef(1, 0, 0)),
                ]),
        };

        var rule = new ContractCompatibilityRule(TimeProvider.System);
        var findings = rule.Validate(contract, BuildContext()).ToList();

        findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Warning &&
            f.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
        findings[0].RuleId.Should().Be(ContractCompatibilityRule.RuleId);
        findings[0].FieldRef.Should().Be("/version/versionHistory");
    }

    [Fact]
    public void Deprecated_without_replacedBy_when_successor_exists_fires_info()
    {
        // v1.0.0 is Active on the resource (the implicit successor); v0.9.0 is
        // Deprecated but carries no replacedBy.
        var contract = ResourceFactory.BuildMessageContract() with
        {
            Version = new SemanticVersion(
                Major: 1,
                Minor: 0,
                Patch: 0,
                Compatibility: CompatibilityIndicator.Backward,
                VersionHistory:
                [
                    new HistoricalVersionEntry(0, 9, 0, LifecycleState.Deprecated),
                ]),
        };

        var rule = new ContractCompatibilityRule(TimeProvider.System);
        var findings = rule.Validate(contract, BuildContext()).ToList();

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Info);
        findings[0].Message.Should().Contain("no replacedBy");
        findings[0].FieldRef.Should().Be("/version/versionHistory");
    }

    [Fact]
    public void Deprecated_with_replacedBy_pointing_at_unknown_version_fires_warning()
    {
        var contract = ResourceFactory.BuildMessageContract() with
        {
            Version = new SemanticVersion(
                Major: 1,
                Minor: 0,
                Patch: 0,
                Compatibility: CompatibilityIndicator.Backward,
                VersionHistory:
                [
                    new HistoricalVersionEntry(
                        0, 9, 0,
                        LifecycleState.Deprecated,
                        ReplacedBy: new SemanticVersionRef(2, 0, 0)),
                ]),
        };

        var rule = new ContractCompatibilityRule(TimeProvider.System);
        var findings = rule.Validate(contract, BuildContext()).ToList();

        findings.Should().ContainSingle(f =>
            f.Severity == ValidationSeverity.Warning &&
            f.Message.Contains("does not resolve", StringComparison.Ordinal));
    }

    [Fact]
    public void Older_version_still_active_in_history_fires_info()
    {
        // v0.9.0 is older than the current Active v1.0.0 but is still marked
        // Active in history — almost certainly an oversight per FR-007.
        var contract = ResourceFactory.BuildMessageContract() with
        {
            Version = new SemanticVersion(
                Major: 1,
                Minor: 0,
                Patch: 0,
                Compatibility: CompatibilityIndicator.Backward,
                VersionHistory:
                [
                    new HistoricalVersionEntry(0, 9, 0, LifecycleState.Active),
                ]),
        };

        var rule = new ContractCompatibilityRule(TimeProvider.System);
        var findings = rule.Validate(contract, BuildContext()).ToList();

        findings.Should().ContainSingle();
        findings[0].Severity.Should().Be(ValidationSeverity.Info);
        findings[0].Message.Should().Contain("older");
    }

    [Fact]
    public void Non_MessageContract_resource_yields_nothing()
    {
        var queue = ResourceFactory.BuildQueue();

        var rule = new ContractCompatibilityRule(TimeProvider.System);
        var findings = rule.Validate(queue, BuildContext()).ToList();

        findings.Should().BeEmpty();
    }

    private static ValidationContext BuildContext() => new()
    {
        RelationshipResolver = _ => null,
        DuplicateDetector = _ => false,
        Services = new EmptyServiceProvider(),
        PreviousLifecycle = null,
    };

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
