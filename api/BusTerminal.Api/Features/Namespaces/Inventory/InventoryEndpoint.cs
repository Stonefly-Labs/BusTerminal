using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Namespaces.Inventory;

// Spec 008 / T102 / US2. GET /api/namespaces. Cross-environment inventory of
// `source = Onboarded` namespaces with filter + sort + paging. Archived hidden
// by default per FR-019; `includeArchived=true` surfaces them.
//
// AuthN-only — any authenticated tenant user can browse the inventory.
public static class InventoryEndpoint
{
    public static IEndpointRouteBuilder MapInventoryEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/namespaces", HandleAsync)
            .RequireAuthorization()
            .WithName("NamespaceInventoryList")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string? environment,
        string[]? lifecycleStatus,
        string[]? validationStatus,
        string? tagKey,
        string? tagValue,
        string? q,
        string? sort,
        bool? includeArchived,
        int? pageSize,
        string? continuationToken,
        IRegistryEntityStore entityStore,
        NamespaceDtoMapping mapping,
        CancellationToken cancellationToken)
    {
        // Parse multi-value enums; reject malformed values with 400.
        var lifecycleParsed = TryParseEnums<LifecycleStatus>(lifecycleStatus, out var lifecycleError);
        if (lifecycleError is not null) return BadRequest(context, "InvalidLifecycleStatus", lifecycleError);

        var validationParsed = TryParseEnums<ValidationStatus>(validationStatus, out var validationError);
        if (validationError is not null) return BadRequest(context, "InvalidValidationStatus", validationError);

        var sortParsed = ParseSort(sort);
        if (sortParsed is null) return BadRequest(context, "InvalidSort",
            $"'{sort}' is not a supported sort key.");

        var clampedPageSize = Math.Clamp(pageSize ?? 25, 1, 100);

        var query = new NamespaceInventoryQuery(
            Environment: string.IsNullOrWhiteSpace(environment) ? null : environment,
            LifecycleStatuses: lifecycleParsed,
            ValidationStatuses: validationParsed,
            TagKey: string.IsNullOrWhiteSpace(tagKey) ? null : tagKey,
            TagValue: string.IsNullOrWhiteSpace(tagValue) ? null : tagValue,
            Search: string.IsNullOrWhiteSpace(q) ? null : q,
            Sort: sortParsed.Value,
            IncludeArchived: includeArchived ?? false,
            PageSize: clampedPageSize,
            ContinuationToken: continuationToken);

        var page = await entityStore.ListOnboardedAsync(query, cancellationToken).ConfigureAwait(false);
        var items = page.Items.Select(mapping.ToResponse).ToArray();
        var response = new InventoryListResponse(items, page.ContinuationToken);
        return Results.Json(response, statusCode: StatusCodes.Status200OK);
    }

    private static IReadOnlyList<T>? TryParseEnums<T>(string[]? values, out string? error)
        where T : struct, Enum
    {
        error = null;
        if (values is null || values.Length == 0) return null;
        var parsed = new List<T>(values.Length);
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (!Enum.TryParse<T>(v, ignoreCase: true, out var result))
            {
                error = $"'{v}' is not a valid {typeof(T).Name} value.";
                return null;
            }
            parsed.Add(result);
        }
        return parsed.Count == 0 ? null : parsed;
    }

    private static NamespaceInventorySort? ParseSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return NamespaceInventorySort.LastValidatedAtDesc;
        return sort switch
        {
            "lastValidatedAt_desc" => NamespaceInventorySort.LastValidatedAtDesc,
            "lastValidatedAt_asc" => NamespaceInventorySort.LastValidatedAtAsc,
            "displayName_asc" => NamespaceInventorySort.DisplayNameAsc,
            "displayName_desc" => NamespaceInventorySort.DisplayNameDesc,
            "environment_asc" => NamespaceInventorySort.EnvironmentAsc,
            "environment_desc" => NamespaceInventorySort.EnvironmentDesc,
            _ => null,
        };
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

// Spec 008 / contracts/namespace-onboarding-api.yaml#/InventoryListResponse.
public sealed record InventoryListResponse(
    IReadOnlyList<NamespaceResponse> Items,
    string? ContinuationToken);
