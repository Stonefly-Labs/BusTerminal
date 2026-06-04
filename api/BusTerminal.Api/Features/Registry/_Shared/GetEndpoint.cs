using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T076 / FR-026. GET /api/registry/{id}.
// Strategy:
//   - When `?environment=X` is provided, do a Cosmos point-read against the
//     correct partition (cheap, deterministic).
//   - Otherwise, fall back to FindByIdAsync (cross-partition with TOP 1).
// Both code paths exclude tombstone documents (research §10) — a
// recently-deleted entity MUST surface as 404, never as the tombstone shape.
public static class GetEndpoint
{
    public static RouteGroupBuilder MapGetRegistryEntity(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", HandleAsync).WithName("RegistryGetById");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid id,
        string? environment,
        IRegistryEntityStore store,
        CancellationToken cancellationToken)
    {
        var entity = string.IsNullOrEmpty(environment)
            ? await store.FindByIdAsync(id, cancellationToken).ConfigureAwait(false)
            : await store.GetAsync(id, environment, cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            return RegistryProblemFactory.NotFound(
                "NotFound", "Entity not found",
                $"No registry entity with id {id} was found.",
                context.Request.Path);
        }

        if (!string.IsNullOrEmpty(entity.Etag))
        {
            context.Response.Headers.ETag = entity.Etag;
        }
        return Results.Json(entity, RegistryJsonOptions.Default, statusCode: 200);
    }
}
