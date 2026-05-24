using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Domain.Validation.Rules;
using BusTerminal.Api.Tests.Unit.Serialization;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Validation.Rules;

// Spec 004 / T127 / FR-010 + FR-013 + Q1. Tests the rule's integration with
// the validation context — specifically that PreviousLifecycle is the only
// trigger for the rule firing, and that it dispatches through
// LifecycleTransitions.IsTransitionLegal so the legal-transition contract
// stays authoritative.
public sealed class LifecycleTransitionRuleTests
{
    [Fact]
    public void Create_mode_emits_no_finding_when_PreviousLifecycle_is_null()
    {
        var queue = ResourceFactory.BuildQueue() with { Lifecycle = LifecycleState.Active };
        var rule = new LifecycleTransitionRule(TimeProvider.System);

        var findings = rule.Validate(queue, BuildContext(previous: null)).ToList();

        findings.Should().BeEmpty(
            "create writes carry no prior state — the rule is silent on creation");
    }

    [Fact]
    public void Same_state_update_emits_no_finding()
    {
        var queue = ResourceFactory.BuildQueue() with { Lifecycle = LifecycleState.Active };
        var rule = new LifecycleTransitionRule(TimeProvider.System);

        var findings = rule.Validate(queue, BuildContext(previous: LifecycleState.Active)).ToList();

        findings.Should().BeEmpty(
            "an update that does not change Lifecycle is a no-op transition");
    }

    [Theory]
    [InlineData(LifecycleState.Draft, LifecycleState.Active)]
    [InlineData(LifecycleState.Active, LifecycleState.Deprecated)]
    [InlineData(LifecycleState.Deprecated, LifecycleState.Active)]
    [InlineData(LifecycleState.Deprecated, LifecycleState.Retired)]
    [InlineData(LifecycleState.Retired, LifecycleState.Archived)]
    public void Legal_transitions_emit_no_finding(LifecycleState previous, LifecycleState target)
    {
        var queue = ResourceFactory.BuildQueue() with { Lifecycle = target };
        var rule = new LifecycleTransitionRule(TimeProvider.System);

        var findings = rule.Validate(queue, BuildContext(previous: previous)).ToList();

        findings.Should().BeEmpty(
            $"{previous} -> {target} is in the legal-transition graph");
    }

    [Theory]
    [InlineData(LifecycleState.Active, LifecycleState.Draft)]
    [InlineData(LifecycleState.Active, LifecycleState.Retired)]
    [InlineData(LifecycleState.Active, LifecycleState.Archived)]
    [InlineData(LifecycleState.Draft, LifecycleState.Deprecated)]
    [InlineData(LifecycleState.Retired, LifecycleState.Active)]
    [InlineData(LifecycleState.Archived, LifecycleState.Active)]
    public void Illegal_transitions_emit_error_finding(LifecycleState previous, LifecycleState target)
    {
        var queue = ResourceFactory.BuildQueue() with { Lifecycle = target };
        var rule = new LifecycleTransitionRule(TimeProvider.System);

        var findings = rule.Validate(queue, BuildContext(previous: previous)).ToList();

        findings.Should().ContainSingle();
        findings[0].RuleId.Should().Be(LifecycleTransitionRule.RuleId);
        findings[0].Severity.Should().Be(ValidationSeverity.Error);
        findings[0].FieldRef.Should().Be("/lifecycle");
        findings[0].Message.Should().Contain(previous.ToString());
        findings[0].Message.Should().Contain(target.ToString());
    }

    [Fact]
    public async Task Engine_passes_previous_lifecycle_into_context()
    {
        // Smoke-test that ValidationEngine.ValidateAsync threads the
        // previousLifecycle argument into ValidationContext, since that
        // plumbing is the entire reason this rule works at scale.
        var queue = ResourceFactory.BuildQueue() with { Lifecycle = LifecycleState.Archived };
        var rule = new LifecycleTransitionRule(TimeProvider.System);

        var engine = new ValidationEngine(
            rules: [rule],
            relationshipRules: [],
            services: new EmptyServices(),
            logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationEngine>.Instance,
            time: TimeProvider.System);

        var result = await engine.ValidateAsync(
            queue,
            relationshipResolver: _ => null,
            duplicateDetector: _ => false,
            previousLifecycle: LifecycleState.Active);

        result.HasErrors.Should().BeTrue("Active -> Archived is illegal and must surface as an Error");
        result.Findings.Should().ContainSingle(f => f.RuleId == LifecycleTransitionRule.RuleId);
    }

    private static ValidationContext BuildContext(LifecycleState? previous) => new()
    {
        RelationshipResolver = _ => null,
        DuplicateDetector = _ => false,
        Services = new EmptyServices(),
        PreviousLifecycle = previous,
    };

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
