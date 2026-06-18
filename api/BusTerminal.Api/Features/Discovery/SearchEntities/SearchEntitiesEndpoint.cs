using Azure;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Features.Discovery.Shared.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Discovery.SearchEntities;

// Spec 009 / T070 + contracts/openapi.yaml#searchEntities.
// GET /api/entities — published-entity catalog search.
//
// Forwards URL query parameters to the IPublishedEntitySearchClient adapter
// after validation + clamping. Mirrors the existing Spec 006 SearchEndpoint
// patterns (query-required, page-clamp, 503-on-AI-Search-outage) but uses
// the spec 009 typed shapes (EntityType, LifecycleStatus, EntityServiceRole).
public static class SearchEntitiesEndpoint
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int MaxQueryLength = 1024;

    public static IEndpointRouteBuilder MapSearchEntitiesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/entities", HandleAsync)
            .RequireAuthorization()
            .WithName("SearchEntities")
            .WithTags("Entities");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string? q,
        string? namespaceId,
        string? associatedServiceId,
        string? sort,
        int? page,
        int? pageSize,
        IPublishedEntitySearchClient searchClient,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(q) && q.Length > MaxQueryLength)
        {
            return BadRequest(context, "QueryTooLong",
                $"q must be {MaxQueryLength} characters or fewer.");
        }

        // Multi-value query params — read directly from the raw query string
        // so callers can supply `?entityType=Queue&entityType=Topic` etc.
        var query = context.Request.Query;
        IReadOnlyList<EntityType>? entityTypeFilters;
        if (!TryParseEnums<EntityType>(query["entityType"], out entityTypeFilters, out var badEntityType))
        {
            return BadRequest(context, "InvalidEntityType",
                $"'{badEntityType}' is not a valid EntityType.");
        }

        IReadOnlyList<EntityServiceRole>? roleFilters;
        if (!TryParseEnums<EntityServiceRole>(query["associationRole"], out roleFilters, out var badRole))
        {
            return BadRequest(context, "InvalidAssociationRole",
                $"'{badRole}' is not a valid EntityServiceRole.");
        }

        IReadOnlyList<LifecycleStatus>? lifecycleFilters;
        if (!TryParseEnums<LifecycleStatus>(query["lifecycleStatus"], out lifecycleFilters, out var badStatus))
        {
            return BadRequest(context, "InvalidLifecycleStatus",
                $"'{badStatus}' is not a valid LifecycleStatus.");
        }

        var tags = query["tag"]
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToArray();

        var clampedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var clampedPage = Math.Max(1, page ?? 1);
        var skip = (clampedPage - 1) * clampedPageSize;

        var sortMode = ParseSort(sort);

        var request = new PublishedEntitySearchRequest(
            Query: q,
            EntityTypeFilters: entityTypeFilters,
            NamespaceIdFilter: namespaceId,
            AssociatedServiceIdFilter: associatedServiceId,
            AssociationRoleFilters: roleFilters,
            TagFilters: tags.Length == 0 ? null : tags,
            LifecycleStatusFilters: lifecycleFilters,
            Sort: sortMode,
            Skip: skip,
            Top: clampedPageSize);

        PublishedEntitySearchResults results;
        try
        {
            results = await searchClient.SearchAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status >= 500 || ex.Status == 408)
        {
            return Results.Problem(
                title: "SearchUnavailable",
                detail: "Azure AI Search is unreachable. Browse and detail experiences remain available.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                instance: context.Request.Path,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["code"] = "SearchUnavailable",
                });
        }

        var items = results.Hits
            .Select(hit => new PublishedEntitySummaryDto(
                Id: hit.Id,
                EntityType: hit.EntityType,
                NamespaceId: hit.NamespaceId,
                Name: hit.Name,
                ParentEntityId: hit.ParentEntityId,
                LifecycleStatus: hit.LifecycleStatus,
                LastSeenUtc: hit.LastSeenUtc,
                AssociatedServiceIds: hit.AssociatedServiceIds,
                AssociationRoles: hit.AssociationRoles,
                Tags: hit.Tags))
            .ToArray();

        var response = new SearchEntitiesResponseDto(items, results.TotalCount, clampedPage, clampedPageSize);
        return Results.Ok(response);
    }

    internal static PublishedEntitySearchSort ParseSort(string? token)
    {
        if (string.IsNullOrEmpty(token)) return PublishedEntitySearchSort.NameAsc;
        return token.ToLowerInvariant() switch
        {
            "name_asc" => PublishedEntitySearchSort.NameAsc,
            "name_desc" => PublishedEntitySearchSort.NameDesc,
            "lastseen_asc" => PublishedEntitySearchSort.LastSeenAsc,
            "lastseen_desc" => PublishedEntitySearchSort.LastSeenDesc,
            _ => PublishedEntitySearchSort.NameAsc,
        };
    }

    private static bool TryParseEnums<TEnum>(
        Microsoft.Extensions.Primitives.StringValues values,
        out IReadOnlyList<TEnum>? parsed,
        out string? badInput) where TEnum : struct, Enum
    {
        if (values.Count == 0)
        {
            parsed = null;
            badInput = null;
            return true;
        }

        var list = new List<TEnum>(values.Count);
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (!Enum.TryParse<TEnum>(v, ignoreCase: true, out var result))
            {
                parsed = null;
                badInput = v;
                return false;
            }
            list.Add(result);
        }
        parsed = list.Count == 0 ? null : list;
        badInput = null;
        return true;
    }

    private static IResult BadRequest(HttpContext context, string code, string detail)
        => Results.Problem(
            title: code,
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = code,
            });
}
