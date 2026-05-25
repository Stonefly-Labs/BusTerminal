using System.Collections.Frozen;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-010 / Q1. Legal-transition graph from contracts/lifecycle-transitions.md.
// Static; no DI required; safe to call from validation rules and persistence layer alike.
public static class LifecycleTransitions
{
    private static readonly FrozenDictionary<LifecycleState, FrozenSet<LifecycleState>> Legal =
        new Dictionary<LifecycleState, FrozenSet<LifecycleState>>
        {
            [LifecycleState.Draft] = new[] { LifecycleState.Draft, LifecycleState.Active }.ToFrozenSet(),
            [LifecycleState.Active] = new[] { LifecycleState.Active, LifecycleState.Deprecated }.ToFrozenSet(),
            [LifecycleState.Deprecated] = new[] { LifecycleState.Deprecated, LifecycleState.Active, LifecycleState.Retired }.ToFrozenSet(),
            [LifecycleState.Retired] = new[] { LifecycleState.Retired, LifecycleState.Archived }.ToFrozenSet(),
            [LifecycleState.Archived] = FrozenSet<LifecycleState>.Empty,
        }.ToFrozenDictionary();

    public static bool IsTransitionLegal(LifecycleState from, LifecycleState to) =>
        Legal[from].Contains(to);

    public static IEnumerable<LifecycleState> LegalSuccessors(LifecycleState from) =>
        Legal[from];
}
