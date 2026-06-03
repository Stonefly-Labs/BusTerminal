namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T039 / FR-009 + FR-013. Bridges the persistence-layer
// `CountChildrenAsync` to the wire shape `HasChildrenResponse`. Used by the
// DELETE endpoint (T079) and by the FluentValidation rule
// `ChildlessOnDeleteRule` (T026 + T074).
//
// Stateless — registered as a singleton.
public sealed class ChildCountChecker
{
    private readonly IRegistryEntityStore _store;

    public ChildCountChecker(IRegistryEntityStore store)
    {
        _store = store;
    }

    public async Task<HasChildrenResponse?> CheckAsync(
        Guid entityId,
        string environment,
        string? instance,
        CancellationToken cancellationToken)
    {
        var breakdown = await _store.CountChildrenAsync(entityId, environment, cancellationToken)
            .ConfigureAwait(false);

        if (breakdown.TotalChildren == 0)
        {
            return null;
        }

        return new HasChildrenResponse(
            EntityId: entityId,
            TotalChildren: breakdown.TotalChildren,
            ChildrenByType: breakdown.ChildrenByType,
            Detail: $"Entity {entityId} has {breakdown.TotalChildren} child entit{(breakdown.TotalChildren == 1 ? "y" : "ies")}; delete them first or change ownership.",
            Instance: instance);
    }
}
