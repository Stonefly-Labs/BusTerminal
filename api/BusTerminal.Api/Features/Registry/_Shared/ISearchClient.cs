namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T036 / data-model.md §6.3. Adapter port for the AI Search
// registry index. Implemented by AzureAiSearchClient
// (Infrastructure/Search/AzureAiSearchClient.cs).
//
// Keeping this contract in the feature folder (rather than in
// Infrastructure/Search/) puts the seam at the right boundary for vertical
// slice architecture — endpoints depend on this interface, the adapter is the
// only thing in Infrastructure/.
public interface ISearchClient
{
    Task<RegistrySearchResults> SearchAsync(
        RegistrySearchRequest request,
        CancellationToken cancellationToken);

    // Reserved for the cmdk typeahead in the explorer/global search bar.
    // Phase 4 US2 wires the call site (T112).
    Task<IReadOnlyList<RegistrySuggestion>> SuggestAsync(
        string partialText,
        int top,
        string? environmentFilter,
        CancellationToken cancellationToken);
}

// Spec 006 / contracts/registry-api.yaml#components.schemas.SearchRequest.
// Per-field nullability matches the OpenAPI shape.
public sealed record RegistrySearchRequest(
    string? Query = null,
    RegistryEntityType? EntityTypeFilter = null,
    string? EnvironmentFilter = null,
    RegistryEntityStatus? StatusFilter = null,
    IReadOnlyList<string>? TagKeysAnyLower = null,
    IReadOnlyList<RegistryTag>? TagsAny = null,
    int Skip = 0,
    int Top = 50,
    RegistrySearchSort Sort = RegistrySearchSort.Relevance);

// Stable sort ordering per research §13. `Relevance` defaults to BM25 score
// desc with `updatedAtUtc desc, id asc` tiebreakers. The other modes apply a
// direct sort and keep `id asc` as the tiebreaker.
public enum RegistrySearchSort
{
    Relevance,
    UpdatedAtDesc,
    UpdatedAtAsc,
    NameAsc,
    NameDesc,
}

public sealed record RegistrySearchResults(
    IReadOnlyList<RegistrySearchHit> Hits,
    long? TotalCount);

public sealed record RegistrySearchHit(
    Guid Id,
    RegistryEntityType EntityType,
    string Name,
    string? FullyQualifiedName,
    string? Environment,
    string? Status,
    string? Owner,
    string? NamespaceName,
    Guid? ParentId,
    double? Score);

public sealed record RegistrySuggestion(
    Guid Id,
    string Suggested,
    RegistryEntityType EntityType,
    string Environment);
