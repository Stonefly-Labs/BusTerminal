using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-006. Matches contracts/resources/subscription.schema.json.
public sealed record Subscription : Resource
{
    public required ResourceId ParentTopicId { get; init; }

    public FilterDefinition? Filter { get; init; }

    public RuleDefinition? Rule { get; init; }

    public IReadOnlyCollection<ApplicationReference> Consumers { get; init; } = [];

    public required DeliverySemantics DeliverySemantics { get; init; }

    public DeadLetterPolicy? DeadLetter { get; init; }

    public RetryPolicy? Retry { get; init; }

    public JsonElement? OperationalMetadata { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<FilterKind>))]
public enum FilterKind
{
    Sql,
    Correlation,
    Boolean,
}

public sealed record FilterDefinition(FilterKind Kind, string Expression);

public sealed record RuleDefinition(string Kind, string Expression);

[JsonConverter(typeof(JsonStringEnumConverter<DeliverySemantics>))]
public enum DeliverySemantics
{
    AtLeastOnce,
    AtMostOnce,
}

public sealed record RetryPolicy(
    int MaxAttempts,
    double InitialBackoffSeconds,
    double MaxBackoffSeconds);
