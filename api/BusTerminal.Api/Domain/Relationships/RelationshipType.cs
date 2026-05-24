using System.Text.Json.Serialization;
using BusTerminal.Api.Domain.Resources;

namespace BusTerminal.Api.Domain.Relationships;

// Spec 004 / FR-008 / T101. Closed enum of relationship types for v1, matching
// contracts/relationship-types.md and the `type` enum in relationship.schema.json.
// Wire form is lower-camelCase.
[JsonConverter(typeof(JsonStringEnumConverter<RelationshipType>))]
public enum RelationshipType
{
    PublishesTo,
    ConsumedBy,
    SubscriptionOf,
    UsesContract,
    Owns,
    AttachedTo,
    Replaces,
    PartOfFlow,
}

// Hand-maintained pairing table per tech-stack.md §1 (explicit over reflection).
// Each entry declares the allowed source resource types and target resource types
// for a given RelationshipType. Used by RelationshipTypeValidityRule (T107).
//
// `Any` semantics: `replaces` allows any source/target with the constraint that
// both endpoints share the same resource type; `attachedTo` allows any target.
// Those cases are signalled by an empty allowed-set and a flag.
public static class RelationshipPairings
{
    public sealed record Rule(
        IReadOnlySet<Type> AllowedSourceTypes,
        IReadOnlySet<Type> AllowedTargetTypes,
        bool AllowAnyTarget = false,
        bool AllowAnySource = false,
        bool RequireMatchingEndpointTypes = false,
        bool AllowSelfRelationship = false);

    private static readonly HashSet<Type> OperationalTypes =
    [
        typeof(Broker),
        typeof(Queue),
        typeof(Topic),
        typeof(Subscription),
        typeof(MessageContract),
        typeof(ProducerApplication),
        typeof(ConsumerApplication),
        typeof(IntegrationFlow),
    ];

    private static readonly IReadOnlyDictionary<RelationshipType, Rule> Table =
        new Dictionary<RelationshipType, Rule>
        {
            [RelationshipType.PublishesTo] = new(
                AllowedSourceTypes: new HashSet<Type> { typeof(ProducerApplication) },
                AllowedTargetTypes: new HashSet<Type> { typeof(Queue), typeof(Topic) }),

            [RelationshipType.ConsumedBy] = new(
                AllowedSourceTypes: new HashSet<Type> { typeof(Queue), typeof(Subscription) },
                AllowedTargetTypes: new HashSet<Type> { typeof(ConsumerApplication) }),

            [RelationshipType.SubscriptionOf] = new(
                AllowedSourceTypes: new HashSet<Type> { typeof(Subscription) },
                AllowedTargetTypes: new HashSet<Type> { typeof(Topic) }),

            [RelationshipType.UsesContract] = new(
                AllowedSourceTypes: new HashSet<Type> { typeof(Queue), typeof(Topic) },
                AllowedTargetTypes: new HashSet<Type> { typeof(MessageContract) }),

            [RelationshipType.Owns] = new(
                AllowedSourceTypes: new HashSet<Type> { typeof(Team) },
                AllowedTargetTypes: OperationalTypes),

            [RelationshipType.AttachedTo] = new(
                AllowedSourceTypes: new HashSet<Type> { typeof(DocumentationAsset) },
                AllowedTargetTypes: new HashSet<Type>(),
                AllowAnyTarget: true),

            [RelationshipType.Replaces] = new(
                AllowedSourceTypes: new HashSet<Type>(),
                AllowedTargetTypes: new HashSet<Type>(),
                AllowAnySource: true,
                AllowAnyTarget: true,
                RequireMatchingEndpointTypes: true),

            [RelationshipType.PartOfFlow] = new(
                AllowedSourceTypes: new HashSet<Type>
                {
                    typeof(ProducerApplication),
                    typeof(ConsumerApplication),
                    typeof(Queue),
                    typeof(Topic),
                },
                AllowedTargetTypes: new HashSet<Type> { typeof(IntegrationFlow) }),
        };

    public static Rule For(RelationshipType type)
    {
        if (Table.TryGetValue(type, out var rule))
        {
            return rule;
        }

        throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            "Unknown RelationshipType — register the pairing rule in RelationshipPairings.Table.");
    }
}
