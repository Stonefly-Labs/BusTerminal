using BusTerminal.Api.Domain;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Domain;

// Spec 004 / T126 / FR-010 / Q1. Locks the full transition matrix from
// contracts/lifecycle-transitions.md against LifecycleTransitions.IsTransitionLegal.
// The matrix is exhaustive: every (from, to) pair across the 5 states is
// asserted. When the contract diagram changes, this test fails loudly and the
// diagram + code + tests must move together.
public sealed class LifecycleTransitionsTests
{
    public static IEnumerable<object[]> LegalTransitions() =>
    [
        [LifecycleState.Draft, LifecycleState.Draft],
        [LifecycleState.Draft, LifecycleState.Active],
        [LifecycleState.Active, LifecycleState.Active],
        [LifecycleState.Active, LifecycleState.Deprecated],
        [LifecycleState.Deprecated, LifecycleState.Deprecated],
        [LifecycleState.Deprecated, LifecycleState.Active],
        [LifecycleState.Deprecated, LifecycleState.Retired],
        [LifecycleState.Retired, LifecycleState.Retired],
        [LifecycleState.Retired, LifecycleState.Archived],
    ];

    public static IEnumerable<object[]> IllegalTransitions() =>
    [
        [LifecycleState.Draft, LifecycleState.Deprecated],
        [LifecycleState.Draft, LifecycleState.Retired],
        [LifecycleState.Draft, LifecycleState.Archived],
        [LifecycleState.Active, LifecycleState.Draft],
        [LifecycleState.Active, LifecycleState.Retired],
        [LifecycleState.Active, LifecycleState.Archived],
        [LifecycleState.Deprecated, LifecycleState.Draft],
        [LifecycleState.Deprecated, LifecycleState.Archived],
        [LifecycleState.Retired, LifecycleState.Draft],
        [LifecycleState.Retired, LifecycleState.Active],
        [LifecycleState.Retired, LifecycleState.Deprecated],
        [LifecycleState.Archived, LifecycleState.Draft],
        [LifecycleState.Archived, LifecycleState.Active],
        [LifecycleState.Archived, LifecycleState.Deprecated],
        [LifecycleState.Archived, LifecycleState.Retired],
        [LifecycleState.Archived, LifecycleState.Archived],
    ];

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public void IsTransitionLegal_returns_true_for_contract_legal_pairs(
        LifecycleState from,
        LifecycleState to)
    {
        LifecycleTransitions.IsTransitionLegal(from, to).Should().BeTrue(
            $"contracts/lifecycle-transitions.md lists {from} -> {to} as legal");
    }

    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public void IsTransitionLegal_returns_false_for_contract_illegal_pairs(
        LifecycleState from,
        LifecycleState to)
    {
        LifecycleTransitions.IsTransitionLegal(from, to).Should().BeFalse(
            $"contracts/lifecycle-transitions.md lists {from} -> {to} as illegal");
    }

    [Fact]
    public void Matrix_covers_every_pair_of_states()
    {
        var all = Enum.GetValues<LifecycleState>();
        var covered = LegalTransitions().Concat(IllegalTransitions())
            .Select(args => ((LifecycleState)args[0], (LifecycleState)args[1]))
            .ToHashSet();

        foreach (var from in all)
        {
            foreach (var to in all)
            {
                covered.Should().Contain((from, to),
                    $"the test data set must enumerate every (from, to) pair — missing ({from}, {to})");
            }
        }
    }

    [Fact]
    public void LegalSuccessors_for_Archived_is_empty()
    {
        LifecycleTransitions.LegalSuccessors(LifecycleState.Archived).Should().BeEmpty(
            "Archived is terminal");
    }

    [Fact]
    public void LegalSuccessors_for_Retired_returns_only_Retired_and_Archived()
    {
        LifecycleTransitions.LegalSuccessors(LifecycleState.Retired).Should().BeEquivalentTo(
            [LifecycleState.Retired, LifecycleState.Archived]);
    }
}
