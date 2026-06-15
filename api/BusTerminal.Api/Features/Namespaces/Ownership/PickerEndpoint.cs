using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Infrastructure.Graph;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Api.Features.Namespaces.Ownership;

// Spec 008 / T078 + research §2 + contracts/namespace-onboarding-api.yaml#/_picker.
// GET /api/namespaces/_picker?q=...&includeGroups=...
// Proxies Microsoft Graph via the workload UAMI; returns up to 25 results,
// display-name-ascending. AuthN-only (no namespace-administrator role) so any
// authenticated user can browse owner candidates before being granted the
// administrator role.
public static partial class PickerEndpoint
{
    private const int MaxResultsPerCall = 25;
    private const int MaxQueryLength = 256;

    [LoggerMessage(
        EventId = 8602,
        Level = LogLevel.Warning,
        Message = "Graph picker query failed; returning 502.")]
    private static partial void LogPickerFailed(ILogger logger, Exception exception);

    public static IEndpointRouteBuilder MapPickerEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/namespaces/_picker", HandleAsync)
            .RequireAuthorization()
            .WithName("NamespacePrincipalPicker")
            .WithTags(NamespaceEndpointsBuilder.GroupTag);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IGraphPrincipalPicker picker,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        string? q = null,
        bool includeGroups = true)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.Problem(
                title: "Query parameter required",
                detail: "Provide a non-empty `q` query parameter.",
                statusCode: StatusCodes.Status400BadRequest,
                instance: context.Request.Path);
        }

        if (q.Length > MaxQueryLength)
        {
            return Results.Problem(
                title: "Query parameter too long",
                detail: $"`q` must be {MaxQueryLength} characters or fewer.",
                statusCode: StatusCodes.Status400BadRequest,
                instance: context.Request.Path);
        }

        try
        {
            var items = await picker
                .SearchAsync(q, MaxResultsPerCall, includeGroups, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new PrincipalPickerResponse(items));
        }
        catch (GraphPickerException ex)
        {
            LogPickerFailed(
                loggerFactory.CreateLogger("BusTerminal.Namespaces.Picker"),
                ex);
            return Results.Problem(
                title: "Picker query failed",
                detail: "Microsoft Graph returned an error. Try again shortly.",
                statusCode: StatusCodes.Status502BadGateway,
                instance: context.Request.Path);
        }
    }
}

// Spec 008 / contracts/namespace-onboarding-api.yaml#/PrincipalPickerResponse.
public sealed record PrincipalPickerResponse(IReadOnlyList<PrincipalPickerItem> Items);
