using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T077 / FR-035 (env-scoped browse). GET /api/registry.
// Environment is REQUIRED — cross-env discovery flows through the search
// endpoint (US2). Tombstones are excluded server-side at the persistence
// layer (CosmosRegistryEntityStore.ListAsync).
public static class ListEndpoint
{
    public static RouteGroupBuilder MapListRegistryEntities(this RouteGroupBuilder group)
    {
        group.MapGet("", HandleAsync).WithName("RegistryList");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        string? environment,
        string? entityType,
        Guid? parentId,
        string? status,
        int? pageSize,
        string? continuationToken,
        IRegistryEntityStore store,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(environment))
        {
            return RegistryProblemFactory.BadRequest(
                "EnvironmentRequired",
                "environment is required",
                "Browse / list queries must be scoped to a single environment per FR-035.",
                context.Request.Path);
        }

        RegistryEntityType? typeFilter = null;
        if (!string.IsNullOrEmpty(entityType))
        {
            if (!Enum.TryParse<RegistryEntityType>(entityType, ignoreCase: true, out var parsed))
            {
                return RegistryProblemFactory.BadRequest(
                    "InvalidEntityType", "Invalid entityType",
                    $"'{entityType}' is not a valid RegistryEntityType.",
                    context.Request.Path);
            }
            typeFilter = parsed;
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

        var clampedPageSize = Math.Clamp(pageSize ?? 200, 1, 500);

        var query = new RegistryEntityListQuery(
            Environment: environment,
            EntityType: typeFilter,
            ParentId: parentId,
            Status: statusFilter,
            PageSize: clampedPageSize,
            ContinuationToken: continuationToken);

        var page = await store.ListAsync(query, cancellationToken).ConfigureAwait(false);
        var response = new RegistryListResponse(page.Items, page.ContinuationToken);
        return Results.Json(response, RegistryJsonOptions.Default, statusCode: 200);
    }
}
