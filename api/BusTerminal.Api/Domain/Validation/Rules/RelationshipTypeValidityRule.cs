using BusTerminal.Api.Domain.Relationships;

namespace BusTerminal.Api.Domain.Validation.Rules;

// Spec 004 / FR-008 / T107. Asserts source/target type pairing per the
// vocabulary in contracts/relationship-types.md (table encoded in
// RelationshipPairings). Self-relationships are illegal in v1 — emits Error.
//
// Implements IRelationshipValidationRule (not IValidationRule) because the
// peer-document Relationship is intentionally NOT a Resource subtype; the
// validation engine dispatches via ValidateRelationshipAsync.
public sealed class RelationshipTypeValidityRule : IRelationshipValidationRule
{
    public const string RuleId = "relationship.typeValidity";

    private readonly TimeProvider _time;

    public RelationshipTypeValidityRule(TimeProvider time)
    {
        _time = time;
    }

    public IEnumerable<ValidationFinding> Validate(Relationship relationship, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(context);

        var now = _time.GetUtcNow();
        var pairing = RelationshipPairings.For(relationship.Type);

        if (!pairing.AllowSelfRelationship && relationship.SourceId == relationship.TargetId)
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Relationship of type '{relationship.Type}' cannot be self-referential (source and target are the same).",
                EvaluatedAt: now,
                FieldRef: "/sourceId",
                RelationshipRef: relationship.SourceId);
        }

        var source = context.RelationshipResolver(relationship.SourceId);
        var target = context.RelationshipResolver(relationship.TargetId);

        if (source is null)
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Relationship sourceId {relationship.SourceId} does not resolve to any resource.",
                EvaluatedAt: now,
                FieldRef: "/sourceId",
                RelationshipRef: relationship.SourceId);
        }

        if (target is null)
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Relationship targetId {relationship.TargetId} does not resolve to any resource.",
                EvaluatedAt: now,
                FieldRef: "/targetId",
                RelationshipRef: relationship.TargetId);
        }

        if (source is not null && !IsAllowedSource(pairing, source.GetType()))
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Relationship of type '{relationship.Type}' does not allow source resource type '{source.ResourceType}'.",
                EvaluatedAt: now,
                FieldRef: "/sourceId",
                RelationshipRef: relationship.SourceId);
        }

        if (target is not null && !IsAllowedTarget(pairing, target.GetType()))
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Relationship of type '{relationship.Type}' does not allow target resource type '{target.ResourceType}'.",
                EvaluatedAt: now,
                FieldRef: "/targetId",
                RelationshipRef: relationship.TargetId);
        }

        if (pairing.RequireMatchingEndpointTypes
            && source is not null
            && target is not null
            && source.GetType() != target.GetType())
        {
            yield return new ValidationFinding(
                RuleId: RuleId,
                Severity: ValidationSeverity.Error,
                Message: $"Relationship of type '{relationship.Type}' requires source and target to share a resource type; got '{source.ResourceType}' and '{target.ResourceType}'.",
                EvaluatedAt: now,
                FieldRef: "/type");
        }
    }

    private static bool IsAllowedSource(RelationshipPairings.Rule pairing, Type sourceType) =>
        pairing.AllowAnySource || pairing.AllowedSourceTypes.Contains(sourceType);

    private static bool IsAllowedTarget(RelationshipPairings.Rule pairing, Type targetType) =>
        pairing.AllowAnyTarget || pairing.AllowedTargetTypes.Contains(targetType);
}
