namespace BusTerminal.Api.Features.Discovery.Shared.Domain;

// Spec 009 / data-model.md §1.2. The full DiscoveryRun record persisted to
// the `discovery-runs` Cosmos container (PK /namespaceId). Append-only after
// terminal status. Mutation is funneled through IDiscoveryRunStore so the
// status transitions stay auditable.
public sealed record DiscoveryRun(
    string Id,
    string SchemaVersion,
    string NamespaceId,
    DiscoveryRunStatus Status,
    DiscoveryTrigger Trigger,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc,
    int? DurationMs,
    string RequestedBy,
    int QueueCount,
    int TopicCount,
    int SubscriptionCount,
    int RuleCount,
    int NewCount,
    int UpdatedCount,
    int UnchangedCount,
    int MissingCount,
    DiscoveryRunFailure? Failure,
    IReadOnlyList<CoalescedRequest> CoalescedRequests,
    string CorrelationId)
{
    public const string CurrentSchemaVersion = "1.0";

    public bool IsTerminal => Status is DiscoveryRunStatus.Succeeded or DiscoveryRunStatus.Failed;
}
