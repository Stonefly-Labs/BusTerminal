namespace BusTerminal.Api.Features.Discovery.Shared.Domain;

// Spec 009 / data-model.md §3 — enums shared across the discovery slice.
// String-serialized in Cosmos + AI Search + the OpenAPI surface via
// JsonStringEnumConverter (wired globally by Program.cs).

public enum EntityType
{
    Queue,
    Topic,
    Subscription,
    Rule,
}

public enum LifecycleStatus
{
    Active,
    Missing,
    Archived,
}

public enum EntityServiceRole
{
    Owner,
    Producer,
    Consumer,
}

public enum DiscoveryRunStatus
{
    Queued,
    InProgress,
    Succeeded,
    Failed,
}

public enum DiscoveryTrigger
{
    Manual,
    // Reserved: Scheduled, Webhook
}

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
