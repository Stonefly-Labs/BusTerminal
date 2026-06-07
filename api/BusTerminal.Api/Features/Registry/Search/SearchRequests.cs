using BusTerminal.Api.Features.Registry.Shared;

namespace BusTerminal.Api.Features.Registry.Search;

// Spec 006 / T110 / contracts/registry-api.yaml. Validated request shape for
// GET /api/registry/search. Bound from query parameters by the endpoint
// handler — the handler trims / clamps / normalizes before forwarding to
// ISearchClient.
public sealed record SearchRequestDto(
    string Query,
    RegistryEntityType? EntityType,
    string? Environment,
    RegistryEntityStatus? Status,
    string? TagKey,
    string? TagValue,
    SearchSortMode Sort,
    int Page,
    int PageSize);

// Maps the wire-level sort token to the persistence-level sort enum.
public enum SearchSortMode
{
    Relevance,
    NameAsc,
    UpdatedDesc,
}
