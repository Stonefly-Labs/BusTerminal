using System.Text.Json.Serialization;

namespace BusTerminal.Api.Features.Discovery.Shared.Domain;

// Spec 009 / data-model.md §3 — enums shared across the discovery slice.
// String-serialized in Cosmos + AI Search + the OpenAPI surface via the
// JsonConverter attributes below; the attribute applies at every
// serialization site (controller responses, Cosmos documents, telemetry).

[JsonConverter(typeof(JsonStringEnumConverter<EntityType>))]
public enum EntityType
{
    Queue,
    Topic,
    Subscription,
    Rule,
}

[JsonConverter(typeof(JsonStringEnumConverter<LifecycleStatus>))]
public enum LifecycleStatus
{
    Active,
    Missing,
    Archived,
}

[JsonConverter(typeof(JsonStringEnumConverter<EntityServiceRole>))]
public enum EntityServiceRole
{
    Owner,
    Producer,
    Consumer,
}

[JsonConverter(typeof(JsonStringEnumConverter<DiscoveryRunStatus>))]
public enum DiscoveryRunStatus
{
    Queued,
    InProgress,
    Succeeded,
    Failed,
}

[JsonConverter(typeof(JsonStringEnumConverter<DiscoveryTrigger>))]
public enum DiscoveryTrigger
{
    Manual,
    // Reserved: Scheduled, Webhook
}

[JsonConverter(typeof(JsonStringEnumConverter<DiscoveryFailureCategory>))]
public enum DiscoveryFailureCategory
{
    Authn,
    Authz,
    NotFound,
    Throttled,
    Transport,
    Internal,
    WorkerLost,
    Unknown,
}

[JsonConverter(typeof(JsonStringEnumConverter<DiscoveryPhase>))]
public enum DiscoveryPhase
{
    LockAcquire,
    FetchQueues,
    FetchTopics,
    FetchSubscriptions,
    FetchRules,
    Persist,
    ResultWrite,
}
