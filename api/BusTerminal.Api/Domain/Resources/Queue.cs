using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-004. Matches contracts/resources/queue.schema.json.
public sealed record Queue : Resource
{
    public required string QueueKind { get; init; }

    public DuplicateDetectionPolicy? DuplicateDetection { get; init; }

    public bool RequiresSession { get; init; }

    public required OrderingPolicy Ordering { get; init; }

    public bool Partitioned { get; init; }

    public DeadLetterPolicy? DeadLetterBehavior { get; init; }

    public int? TtlSeconds { get; init; }

    public long? MaxMessageSizeBytes { get; init; }

    public IReadOnlyCollection<ContractReference> ContractAssociations { get; init; } = [];

    public IReadOnlyCollection<ApplicationReference> Producers { get; init; } = [];

    public IReadOnlyCollection<ApplicationReference> Consumers { get; init; } = [];

    public JsonElement? OperationalMetadata { get; init; }

    public DeprecationMetadata? Deprecation { get; init; }
}

public sealed record DuplicateDetectionPolicy(bool Enabled, int WindowSeconds);

[JsonConverter(typeof(JsonStringEnumConverter<OrderingPolicy>))]
public enum OrderingPolicy
{
    Fifo,
    Unordered,
}

[JsonConverter(typeof(JsonStringEnumConverter<ExpirationAction>))]
public enum ExpirationAction
{
    DeadLetter,
    Drop,
}

public sealed record DeadLetterPolicy(int MaxDeliveryCount, ExpirationAction ExpirationAction);

public sealed record ContractReference(ResourceId ContractId);

public sealed record ApplicationReference(ResourceId ApplicationId);

public sealed record DeprecationMetadata(
    DateOnly? ScheduledRetirementDate = null,
    ResourceId? ReplacedByResourceId = null);
