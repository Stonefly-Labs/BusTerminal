using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T103c / FR-035. GET /api/registry/environments — distinct sorted
// list of environments currently present in `registry-entities`. Cached for
// 60s in IMemoryCache so the explorer's environment switcher doesn't issue a
// cross-partition query on every page navigation.
public static class EnvironmentsEndpoint
{
    private const string CacheKey = "registry.environments.distinct";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public static RouteGroupBuilder MapEnvironmentsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/environments", HandleAsync).WithName("RegistryEnvironments");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        IRegistryEntityStore store,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue<EnvironmentsListResponse>(CacheKey, out var cached))
        {
            var distinct = await store.ListDistinctEnvironmentsAsync(cancellationToken)
                .ConfigureAwait(false);
            cached = new EnvironmentsListResponse(distinct);
            cache.Set(CacheKey, cached, CacheTtl);
        }

        return Results.Json(cached, RegistryJsonOptions.Default, statusCode: 200);
    }
}
