namespace BusTerminal.Api.Features.Namespaces.Shared;

// Spec 008 / data-model.md §3 + research §6. Persistence port for the new
// `namespace-validation-runs` Cosmos container (PK /namespaceId, append-only).
// Implementation lives in Infrastructure/Persistence/CosmosNamespaceValidationRunStore.cs.
public interface INamespaceValidationRunStore
{
    Task AppendAsync(ValidationRun run, CancellationToken cancellationToken);

    Task<ValidationRunPage> ListForNamespaceAsync(
        Guid namespaceId,
        int limit,
        string? continuationToken,
        CancellationToken cancellationToken);

    Task<ValidationRun?> GetAsync(
        Guid namespaceId,
        Guid runId,
        CancellationToken cancellationToken);
}

public sealed record ValidationRunPage(
    IReadOnlyList<ValidationRun> Items,
    string? ContinuationToken);
