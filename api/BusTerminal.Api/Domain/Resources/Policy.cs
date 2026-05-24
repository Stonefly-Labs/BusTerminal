namespace BusTerminal.Api.Domain.Resources;

// Spec 004 / FR-002. Matches contracts/resources/policy.schema.json.
public sealed record Policy : Resource
{
    public required string PolicyKind { get; init; }

    public required string RuleBody { get; init; }

    public required PolicyScope Scope { get; init; }
}

public enum PolicyScopeKind
{
    Namespace,
    ResourceType,
    ResourceInstance,
}

public sealed record PolicyScope(
    PolicyScopeKind Kind,
    ResourceId? TargetId = null,
    string? TargetResourceType = null,
    string? TargetNamespacePath = null);
