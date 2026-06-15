using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Namespaces.Validation;

// Spec 008 / T132 + contracts/namespace-onboarding-api.yaml#/validation-runs (GET list + GET single).
// Two endpoints share this file because they're trivial thin wrappers over
// INamespaceValidationRunStore — keeping them together preserves the
// "single seam per slice" convention of the namespaces feature family.
public static class ListValidationRunsEndpoint
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapListValidationRunsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/namespaces/{id:guid}/validation-runs", HandleListAsync)
            .RequireAuthorization()
            .WithName("NamespaceValidationRunList")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        endpoints
            .MapGet("/api/namespaces/{id:guid}/validation-runs/{runId:guid}", HandleGetAsync)
            .RequireAuthorization()
            .WithName("NamespaceValidationRunGet")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleListAsync(
        HttpContext context,
        Guid id,
        IRegistryEntityStore entityStore,
        INamespaceValidationRunStore runStore,
        CancellationToken cancellationToken,
        int? pageSize = null,
        string? continuationToken = null)
    {
        var current = await entityStore.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (current is not RegistryNamespace ns || ns.Source != RegistrySource.Onboarded)
        {
            return Problem(StatusCodes.Status404NotFound, "NotFound",
                $"No onboarded namespace with id {id:D} was found.", context.Request.Path);
        }

        var clamped = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var page = await runStore
            .ListForNamespaceAsync(ns.Id, clamped, continuationToken, cancellationToken)
            .ConfigureAwait(false);

        return Results.Json(
            new ValidationRunListResponse(page.Items, page.ContinuationToken),
            statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> HandleGetAsync(
        HttpContext context,
        Guid id,
        Guid runId,
        IRegistryEntityStore entityStore,
        INamespaceValidationRunStore runStore,
        CancellationToken cancellationToken)
    {
        var current = await entityStore.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (current is not RegistryNamespace ns || ns.Source != RegistrySource.Onboarded)
        {
            return Problem(StatusCodes.Status404NotFound, "NotFound",
                $"No onboarded namespace with id {id:D} was found.", context.Request.Path);
        }

        var run = await runStore.GetAsync(ns.Id, runId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Problem(StatusCodes.Status404NotFound, "NotFound",
                $"No validation run with id {runId:D} was found for namespace {id:D}.",
                context.Request.Path);
        }

        return Results.Json(run, statusCode: StatusCodes.Status200OK);
    }

    private static IResult Problem(int status, string code, string detail, string instance)
        => Results.Problem(
            title: code,
            detail: detail,
            statusCode: status,
            instance: instance,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = code,
            });
}

// Spec 008 / contracts/namespace-onboarding-api.yaml#/ValidationRunListResponse.
public sealed record ValidationRunListResponse(
    IReadOnlyList<ValidationRun> Items,
    string? ContinuationToken);
