namespace BusTerminal.Api.Domain.Lifecycle;

// Spec 004 / FR-020 / T122. Pure helpers — no I/O, no persistence concerns.
// The store layer composes these into its SoftDeleteAsync / RestoreAsync paths
// and adds audit stamping + change-event emission around the marker flip.
//
// MarkDeleted preserves the existing Lifecycle: soft-delete is orthogonal to
// lifecycle state per contracts/lifecycle-transitions.md § "Soft-delete and
// restoration are NOT lifecycle transitions". MarkRestored takes the state to
// restore to as an explicit argument — in practice that is the existing
// document's preserved Lifecycle, but the explicit parameter lets the store
// recover the right state even if a future path edits Lifecycle while the
// document is in a soft-deleted condition.
public static class SoftDelete
{
    public static Resource MarkDeleted(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return resource with { IsDeleted = true };
    }

    public static Resource MarkRestored(Resource resource, LifecycleState restoredState)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return resource with
        {
            IsDeleted = false,
            Lifecycle = restoredState,
        };
    }
}
