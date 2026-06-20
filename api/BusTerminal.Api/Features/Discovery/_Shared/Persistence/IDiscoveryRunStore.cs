using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.Shared.Persistence;

// Spec 009 / T016. Persistence port for the `discovery-runs` Cosmos
// container. PK /namespaceId; runs are append-on-create, mutable only through
// the controlled UpdateStatusAsync transitions until they reach a terminal
// status (Succeeded | Failed) at which point they become immutable.
public interface IDiscoveryRunStore
{
    Task<DiscoveryRun> CreateAsync(DiscoveryRun run, CancellationToken cancellationToken);

    Task<DiscoveryRun?> GetAsync(string runId, string namespaceId, CancellationToken cancellationToken);

    // Reverse-chronological pagination over a single namespace's history.
    // Uses the composite index (/namespaceId, /startedUtc DESC).
    Task<DiscoveryRunPage> ListByNamespaceAsync(
        string namespaceId,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken);

    // Idempotent terminal-status transitions + count + failure capture.
    // Optimistic-concurrency `ifMatch` honored when supplied; null = unconditional.
    Task<DiscoveryRun> UpdateStatusAsync(
        string runId,
        string namespaceId,
        DiscoveryRunStatusUpdate update,
        string? ifMatch,
        CancellationToken cancellationToken);

    // Append a coalesced-request entry to the run's audit array. Used by
    // DiscoveryRunCoalescer when an in-flight run absorbs a new request.
    Task AppendCoalescedRequestAsync(
        string runId,
        string namespaceId,
        CoalescedRequest request,
        CancellationToken cancellationToken);
}

// Patch surface for run status transitions. All fields are optional —
// nulls leave the corresponding fields untouched on the persisted document.
public sealed record DiscoveryRunStatusUpdate(
    DiscoveryRunStatus? Status = null,
    DateTimeOffset? CompletedUtc = null,
    int? DurationMs = null,
    int? QueueCount = null,
    int? TopicCount = null,
    int? SubscriptionCount = null,
    int? RuleCount = null,
    int? NewCount = null,
    int? UpdatedCount = null,
    int? UnchangedCount = null,
    int? MissingCount = null,
    DiscoveryRunFailure? Failure = null);

public sealed record DiscoveryRunPage(IReadOnlyList<DiscoveryRun> Items, string? ContinuationToken);
