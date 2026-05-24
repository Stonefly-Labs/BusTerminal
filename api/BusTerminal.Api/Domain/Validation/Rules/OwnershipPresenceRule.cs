using BusTerminal.Api.Domain.Resources;

namespace BusTerminal.Api.Domain.Validation.Rules;

// Spec 004 / FR-009 / T094. Applies only to the 8 operational resource types
// (the things that move messages: Broker, Queue, Topic, Subscription, contracts,
// producer/consumer applications, and integration flows). Non-operational types
// (Namespace, Team, Environment, Tag, Policy, DocumentationAsset) carry no
// per-instance ownership — they're either organizational metadata (Team /
// Environment) or governance/documentation peers that inherit context.
//
// Two assertions, both Error severity:
//   1. Ownership is non-null.
//   2. Ownership.OwningTeamId resolves through the relationship resolver to a
//      live Team resource. Missing or wrong-type referents fire dangling-reference
//      Errors. Soft-deleted referents also fail here (US3's DanglingReferenceRule
//      downgrades soft-deleted targets to Warning for *general* references; the
//      narrower ownership contract treats them as broken).
public sealed class OwnershipPresenceRule : IValidationRule
{
    public const string RuleId = "ownership.presence";

    // Hand-maintained set per tech-stack.md §1 (explicit over reflection). When a
    // future operational type lands, register it here.
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

    private readonly TimeProvider _time;

    public OwnershipPresenceRule(TimeProvider time)
    {
        _time = time;
    }

    public bool AppliesTo(Type resourceType) => OperationalTypes.Contains(resourceType);

    public IEnumerable<ValidationFinding> Validate(Resource resource, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(context);

        var now = _time.GetUtcNow();

        if (resource.Ownership is null)
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Operational resource of type '{resource.ResourceType}' must declare ownership.",
                EvaluatedAt: now,
                FieldRef: "/ownership");
            yield break;
        }

        var teamId = resource.Ownership.OwningTeamId;
        var referent = context.RelationshipResolver(teamId);

        if (referent is null)
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Ownership.OwningTeamId {teamId} does not resolve to a Team resource.",
                EvaluatedAt: now,
                FieldRef: "/ownership/owningTeamId",
                RelationshipRef: teamId);
            yield break;
        }

        if (referent is not Team)
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Ownership.OwningTeamId {teamId} resolves to a '{referent.ResourceType}' resource, not a Team.",
                EvaluatedAt: now,
                FieldRef: "/ownership/owningTeamId",
                RelationshipRef: teamId);
            yield break;
        }

        if (referent.IsDeleted)
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Ownership.OwningTeamId {teamId} resolves to a soft-deleted Team; an operational resource cannot be owned by a deleted team.",
                EvaluatedAt: now,
                FieldRef: "/ownership/owningTeamId",
                RelationshipRef: teamId);
        }
    }
}
