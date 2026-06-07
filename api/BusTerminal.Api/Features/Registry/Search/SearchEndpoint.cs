using Azure;
using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Registry.Search;

// Spec 006 / T109 / FR-022..FR-025. GET /api/registry/search.
//   - Validates and clamps query params per contracts/registry-api.yaml.
//   - Translates wire-level (tagKey, tagValue, sort token) to the
//     RegistrySearchRequest the ISearchClient adapter consumes.
//   - Returns 503 RFC-7807 on Azure AI Search outage so browse / detail
//     continue to work (SC-011).
public static class SearchEndpoint
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int MaxQueryLength = 1024;

    public static RouteGroupBuilder MapSearchEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/search", HandleAsync).WithName("RegistrySearch");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string? q,
        string? entityType,
        string? environment,
        string? status,
        string? tagKey,
        string? tagValue,
        string? sort,
        int? page,
        int? pageSize,
        ISearchClient searchClient,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return RegistryProblemFactory.BadRequest(
                "QueryRequired",
                "q is required",
                "Full-text search requires a non-empty `q` query parameter (use `*` to match all).",
                context.Request.Path);
        }
        if (q.Length > MaxQueryLength)
        {
            return RegistryProblemFactory.BadRequest(
                "QueryTooLong",
                "q exceeds maximum length",
                $"q must be {MaxQueryLength} characters or fewer.",
                context.Request.Path);
        }

        RegistryEntityType? entityTypeFilter = null;
        if (!string.IsNullOrEmpty(entityType))
        {
            if (!Enum.TryParse<RegistryEntityType>(entityType, ignoreCase: true, out var parsedType))
            {
                return RegistryProblemFactory.BadRequest(
                    "InvalidEntityType", "Invalid entityType",
                    $"'{entityType}' is not a valid RegistryEntityType.",
                    context.Request.Path);
            }
            entityTypeFilter = parsedType;
        }

        RegistryEntityStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse<RegistryEntityStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                return RegistryProblemFactory.BadRequest(
                    "InvalidStatus", "Invalid status",
                    $"'{status}' is not a valid RegistryEntityStatus.",
                    context.Request.Path);
            }
            statusFilter = parsedStatus;
        }

        if (!string.IsNullOrEmpty(tagValue) && string.IsNullOrEmpty(tagKey))
        {
            return RegistryProblemFactory.BadRequest(
                "TagValueRequiresKey",
                "tagValue requires tagKey",
                "tagValue must be paired with a tagKey — value-only filtering is not supported.",
                context.Request.Path);
        }

        var sortMode = ParseSort(sort);
        var registrySort = sortMode switch
        {
            SearchSortMode.Relevance => RegistrySearchSort.Relevance,
            SearchSortMode.NameAsc => RegistrySearchSort.NameAsc,
            SearchSortMode.UpdatedDesc => RegistrySearchSort.UpdatedAtDesc,
            _ => RegistrySearchSort.Relevance,
        };

        var clampedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var clampedPage = Math.Max(1, page ?? 1);
        var skip = (clampedPage - 1) * clampedPageSize;

        IReadOnlyList<string>? tagKeysAnyLower = null;
        IReadOnlyList<RegistryTag>? tagsAny = null;
        if (!string.IsNullOrEmpty(tagKey))
        {
            if (!string.IsNullOrEmpty(tagValue))
            {
                tagsAny = new[] { new RegistryTag(tagKey, tagValue) };
            }
            else
            {
                tagKeysAnyLower = new[] { tagKey.ToLowerInvariant() };
            }
        }

        var request = new RegistrySearchRequest(
            Query: q,
            EntityTypeFilter: entityTypeFilter,
            EnvironmentFilter: environment,
            StatusFilter: statusFilter,
            TagKeysAnyLower: tagKeysAnyLower,
            TagsAny: tagsAny,
            Skip: skip,
            Top: clampedPageSize,
            Sort: registrySort);

        RegistrySearchResults results;
        try
        {
            results = await searchClient.SearchAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status >= 500 || ex.Status == 408)
        {
            return Results.Json(new
            {
                type = RegistryProblemFactory.ProblemBaseUri + "search-unavailable",
                title = "Search backend temporarily unavailable",
                status = 503,
                code = "SearchUnavailable",
                detail = "Azure AI Search is unreachable. Browse and detail experiences remain available.",
                instance = (string?)context.Request.Path,
            }, RegistryJsonOptions.Default, statusCode: 503,
            contentType: RegistryProblemFactory.ProblemContentType);
        }

        var items = results.Hits
            .Select(h => new SearchResultDto(
                Id: h.Id,
                EntityType: h.EntityType,
                Name: h.Name,
                FullyQualifiedName: h.FullyQualifiedName,
                Environment: h.Environment,
                Status: h.Status,
                Owner: h.Owner,
                NamespaceName: h.NamespaceName,
                Score: h.Score))
            .ToArray();

        var response = new SearchResponseDto(items, results.TotalCount, clampedPage, clampedPageSize);
        return Results.Json(response, RegistryJsonOptions.Default, statusCode: 200);
    }

    private static SearchSortMode ParseSort(string? token)
    {
        if (string.IsNullOrEmpty(token)) return SearchSortMode.Relevance;
        return token.ToLowerInvariant() switch
        {
            "relevance" => SearchSortMode.Relevance,
            "name_asc" or "nameasc" or "name-asc" => SearchSortMode.NameAsc,
            "updated_desc" or "updateddesc" or "updated-desc" => SearchSortMode.UpdatedDesc,
            _ => SearchSortMode.Relevance,
        };
    }
}
