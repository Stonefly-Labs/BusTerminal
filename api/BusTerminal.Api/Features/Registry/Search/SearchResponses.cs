using BusTerminal.Api.Features.Registry.Shared;

namespace BusTerminal.Api.Features.Registry.Search;

// Spec 006 / T110 / contracts/registry-api.yaml#components.schemas.SearchResult.
// Wire shape returned by GET /api/registry/search.
public sealed record SearchResultDto(
    Guid Id,
    RegistryEntityType EntityType,
    string Name,
    string? FullyQualifiedName,
    string? Environment,
    string? Status,
    string? Owner,
    string? NamespaceName,
    double? Score);

public sealed record SearchResponseDto(
    IReadOnlyList<SearchResultDto> Items,
    long? TotalCount,
    int Page,
    int PageSize);
