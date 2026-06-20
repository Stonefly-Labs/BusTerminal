using System.Text.Json.Serialization;
using BusTerminal.Api.Features.Discovery.Shared.Domain;

namespace BusTerminal.Api.Features.Discovery.Shared.Persistence;

// Spec 009 / data-model.md §1.2. Cosmos wire shape for the DiscoveryRun
// document — a flat mirror of the in-memory DiscoveryRun record, with a
// separate `Failure` sub-document and the `coalescedRequests` audit array.
internal sealed record DiscoveryRunDocument
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("schemaVersion")] public required string SchemaVersion { get; init; }
    [JsonPropertyName("namespaceId")] public required string NamespaceId { get; init; }
    [JsonPropertyName("status")] public required DiscoveryRunStatus Status { get; init; }
    [JsonPropertyName("trigger")] public required DiscoveryTrigger Trigger { get; init; }
    [JsonPropertyName("startedUtc")] public required DateTimeOffset StartedUtc { get; init; }
    [JsonPropertyName("completedUtc")] public DateTimeOffset? CompletedUtc { get; init; }
    [JsonPropertyName("durationMs")] public int? DurationMs { get; init; }
    [JsonPropertyName("requestedBy")] public required string RequestedBy { get; init; }
    [JsonPropertyName("queueCount")] public int QueueCount { get; init; }
    [JsonPropertyName("topicCount")] public int TopicCount { get; init; }
    [JsonPropertyName("subscriptionCount")] public int SubscriptionCount { get; init; }
    [JsonPropertyName("ruleCount")] public int RuleCount { get; init; }
    [JsonPropertyName("newCount")] public int NewCount { get; init; }
    [JsonPropertyName("updatedCount")] public int UpdatedCount { get; init; }
    [JsonPropertyName("unchangedCount")] public int UnchangedCount { get; init; }
    [JsonPropertyName("missingCount")] public int MissingCount { get; init; }
    [JsonPropertyName("failure")] public DiscoveryRunFailure? Failure { get; init; }
    [JsonPropertyName("coalescedRequests")] public IReadOnlyList<CoalescedRequest>? CoalescedRequests { get; init; }
    [JsonPropertyName("correlationId")] public required string CorrelationId { get; init; }

    public static DiscoveryRunDocument FromDomain(DiscoveryRun run) => new()
    {
        Id = run.Id,
        SchemaVersion = run.SchemaVersion,
        NamespaceId = run.NamespaceId,
        Status = run.Status,
        Trigger = run.Trigger,
        StartedUtc = run.StartedUtc,
        CompletedUtc = run.CompletedUtc,
        DurationMs = run.DurationMs,
        RequestedBy = run.RequestedBy,
        QueueCount = run.QueueCount,
        TopicCount = run.TopicCount,
        SubscriptionCount = run.SubscriptionCount,
        RuleCount = run.RuleCount,
        NewCount = run.NewCount,
        UpdatedCount = run.UpdatedCount,
        UnchangedCount = run.UnchangedCount,
        MissingCount = run.MissingCount,
        Failure = run.Failure,
        CoalescedRequests = run.CoalescedRequests.Count > 0 ? run.CoalescedRequests : null,
        CorrelationId = run.CorrelationId,
    };

    public DiscoveryRun ToDomain() => new(
        Id: Id,
        SchemaVersion: SchemaVersion,
        NamespaceId: NamespaceId,
        Status: Status,
        Trigger: Trigger,
        StartedUtc: StartedUtc,
        CompletedUtc: CompletedUtc,
        DurationMs: DurationMs,
        RequestedBy: RequestedBy,
        QueueCount: QueueCount,
        TopicCount: TopicCount,
        SubscriptionCount: SubscriptionCount,
        RuleCount: RuleCount,
        NewCount: NewCount,
        UpdatedCount: UpdatedCount,
        UnchangedCount: UnchangedCount,
        MissingCount: MissingCount,
        Failure: Failure,
        CoalescedRequests: CoalescedRequests ?? Array.Empty<CoalescedRequest>(),
        CorrelationId: CorrelationId);
}
