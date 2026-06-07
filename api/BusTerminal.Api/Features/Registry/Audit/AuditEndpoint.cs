using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Registry.Audit;

// Spec 006 / T121 [US3] / FR-033..FR-034. GET /api/registry/{id}/audit.
// Returns the most recent audit events for an entity, newest first. Append-only:
// the API exposes NO write surface on this route (FR-034). `limit` is bounded
// 1..200 (default 50) per contracts/registry-api.yaml. Querying an unknown id
// returns an empty list so the detail page can render "no events" without a
// second round-trip.
public static class AuditEndpoint
{
    private const int DefaultLimit = 50;
    private const int MinLimit = 1;
    private const int MaxLimit = 200;

    public static RouteGroupBuilder MapAuditEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/audit", HandleAsync).WithName("RegistryAuditList");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        Guid id,
        int? limit,
        IAuditEventStore auditStore,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = limit ?? DefaultLimit;
        if (effectiveLimit < MinLimit || effectiveLimit > MaxLimit)
        {
            return RegistryProblemFactory.BadRequest(
                "InvalidLimit",
                "limit out of range",
                $"limit must be between {MinLimit} and {MaxLimit} (inclusive). Default is {DefaultLimit}.",
                context.Request.Path);
        }

        var events = await auditStore.ListForEntityAsync(id, effectiveLimit, cancellationToken)
            .ConfigureAwait(false);
        var response = new AuditListResponse(events);
        return Results.Json(response, RegistryJsonOptions.Default, statusCode: 200);
    }
}
