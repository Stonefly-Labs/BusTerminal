using System.Diagnostics;
using BusTerminal.Api.Authorization;
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

    private static Ok<WhoAmIResponse> HandleAsync(
        HttpContext context,
        IHostEnvironment environment,
        IPlatformPrincipalAccessor principalAccessor)
    {
        var caller = principalAccessor.Current;
        var principal = new Principal(
            Oid: caller is null || caller.ObjectId == Guid.Empty
                ? string.Empty
                : caller.ObjectId.ToString(),
            DisplayName: caller?.DisplayName,
            PreferredUsername: caller?.Username,
            TenantId: caller is null || caller.TenantId == Guid.Empty
                ? string.Empty
                : caller.TenantId.ToString(),
            CallerType: (caller?.CallerType ?? CallerType.Human).ToString(),
            EffectiveRoles: caller is null
                ? Array.Empty<string>()
                : caller.EffectiveRoles
                    .Select(r => r.ToClaimValue())
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToArray());

        var receivedTraceparent = context.Request.Headers["traceparent"].ToString();
        var activity = Activity.Current;
        string traceId;
        string spanId;
        if (activity is not null)
        {
            traceId = activity.TraceId.ToHexString();
            spanId = activity.SpanId.ToHexString();
        }
        else if (!string.IsNullOrEmpty(receivedTraceparent)
            && ActivityContext.TryParse(receivedTraceparent, null, out var parentCtx))
        {
            // Fallback when no ActivityListener is registered (e.g. tests without
            // OpenTelemetry exporter wired up): echo the inbound W3C trace context.
            traceId = parentCtx.TraceId.ToHexString();
            spanId = parentCtx.SpanId.ToHexString();
        }
        else
        {
            traceId = string.Empty;
            spanId = string.Empty;
        }
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

public sealed record Principal(
    string Oid,
    string? DisplayName,
    string? PreferredUsername,
    string TenantId,
    string CallerType,
    IReadOnlyList<string> EffectiveRoles);

public sealed record Correlation(string TraceId, string SpanId, string? ReceivedTraceparent);

public sealed record ServerInfo(string Environment, string Revision, DateTime ServerTimeUtc);
