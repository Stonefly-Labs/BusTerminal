using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BusTerminal.Api.Features.Identity;

public static class WhoAmIEndpoint
{
    public const string ContainerAppRevisionEnvVar = "CONTAINER_APP_REVISION";

    public static IEndpointRouteBuilder MapWhoAmIEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/whoami", HandleAsync)
            .RequireAuthorization()
            .WithName("WhoAmI");

        return endpoints;
    }

    private static Ok<WhoAmIResponse> HandleAsync(HttpContext context, IHostEnvironment environment)
    {
        var user = context.User;
        var principal = new Principal(
            Oid: GetClaim(user, "oid")
                ?? GetClaim(user, ClaimTypes.NameIdentifier)
                ?? string.Empty,
            DisplayName: GetClaim(user, "name")
                ?? user.Identity?.Name
                ?? string.Empty,
            PreferredUsername: GetClaim(user, "preferred_username"),
            TenantId: GetClaim(user, "tid") ?? string.Empty);

        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToHexString() ?? string.Empty;
        var spanId = activity?.SpanId.ToHexString() ?? string.Empty;
        var receivedTraceparent = context.Request.Headers["traceparent"].ToString();
        var correlation = new Correlation(
            TraceId: traceId,
            SpanId: spanId,
            ReceivedTraceparent: string.IsNullOrEmpty(receivedTraceparent) ? null : receivedTraceparent);

        var server = new ServerInfo(
            Environment: NormalizeEnvironmentName(environment.EnvironmentName),
            Revision: Environment.GetEnvironmentVariable(ContainerAppRevisionEnvVar) ?? "local",
            ServerTimeUtc: DateTime.UtcNow);

        return TypedResults.Ok(new WhoAmIResponse(principal, correlation, server));
    }

    private static string? GetClaim(ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirst(claimType)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string NormalizeEnvironmentName(string environmentName) =>
        environmentName.ToLowerInvariant() switch
        {
            "development" => "local",
            "production" => "prod",
            "test" => "test",
            "dev" => "dev",
            _ => environmentName.ToLowerInvariant(),
        };
}

public sealed record WhoAmIResponse(Principal Principal, Correlation Correlation, ServerInfo Server);

public sealed record Principal(string Oid, string DisplayName, string? PreferredUsername, string TenantId);

public sealed record Correlation(string TraceId, string SpanId, string? ReceivedTraceparent);

public sealed record ServerInfo(string Environment, string Revision, DateTime ServerTimeUtc);
