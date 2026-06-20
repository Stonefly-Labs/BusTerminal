namespace BusTerminal.Api.Features.Discovery.Shared.Persistence;

// Spec 009 / T017 + R-03 + data-model.md §1.3. Per-namespace coalescing
// lock. One document per namespace partition with `id = "lock"`; mutation
// uses Cosmos optimistic concurrency (`IfMatch` ETag).
public interface IDiscoveryLockStore
{
    // FR-003 acquisition path. Atomic by ETag. Outcomes:
    //   Acquired       → caller owns the lock; should enqueue the discovery
    //   Coalesced      → an in-flight run exists; caller returns its runId
    //   Stolen         → prior lock holder timed out; caller proceeds and
    //                    must mark the prior run Failed with WorkerLost
    Task<DiscoveryLockAcquisition> TryAcquireAsync(
        string namespaceId,
        string newRunId,
        string podId,
        CancellationToken cancellationToken);

    // Worker-side release. Called when a run reaches a terminal status.
    // Unconditional — the worker is the legitimate holder.
    Task ReleaseAsync(string namespaceId, string runId, CancellationToken cancellationToken);
}

public enum DiscoveryLockOutcome { Acquired, Coalesced, Stolen }

public sealed record DiscoveryLockAcquisition(
    DiscoveryLockOutcome Outcome,
    string ActiveRunId,
    string? StolenRunId);
