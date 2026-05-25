namespace BusTerminal.Api.Domain.Validation;

// Matches validation-result.schema.json#/$defs/finding.
public sealed record ValidationFinding(
    string RuleId,
    ValidationSeverity Severity,
    string Message,
    DateTimeOffset EvaluatedAt,
    string? FieldRef = null,
    ResourceId? RelationshipRef = null);
