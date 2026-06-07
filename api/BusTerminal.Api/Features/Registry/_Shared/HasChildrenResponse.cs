namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / FR-009 / contracts/registry-api.yaml#components.schemas.HasChildrenResponse.
// Returned as the 409 body when DELETE is rejected because the subject has
// children. The per-type breakdown lets the UI tell the operator exactly
// which child entities are in the way.
public sealed record HasChildrenResponse(
    Guid EntityId,
    int TotalChildren,
    IReadOnlyDictionary<RegistryEntityType, int> ChildrenByType,
    string? Detail = null,
    string? Instance = null)
{
    public string Type { get; } = "https://busterminal.dev/probs/has-children";
    public string Title { get; } = "Cannot delete entity with children";
    public int Status { get; } = 409;
    public string Code { get; } = "HasChildren";
}
