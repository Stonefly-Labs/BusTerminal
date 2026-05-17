using System.Net.Http;
using BusTerminal.Api.Infrastructure.Authentication;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.Uris;

namespace BusTerminal.Api.Features.Health;

public static class HealthEndpoints
{
    public const string EntraMetadataCheck = "entra-metadata";
    public const string KeyVaultCheck = "key-vault";
    public const string StartupReadyCheck = "startup-entra-metadata";

    public static IServiceCollection AddBusTerminalHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var azureAd = configuration.GetSection("AzureAd");
        var instance = azureAd["Instance"] ?? "https://login.microsoftonline.com/";
        var tenantId = azureAd["TenantId"];
        var keyVaultUri = Environment.GetEnvironmentVariable(
            Infrastructure.Configuration.KeyVaultExtensions.KeyVaultUriEnvironmentVariable);

        var builder = services.AddHealthChecks();

        if (!string.IsNullOrWhiteSpace(tenantId)
            && !string.Equals(tenantId, AuthenticationExtensions.DevelopmentTenantSentinel, StringComparison.Ordinal))
        {
            var metadataUri = new Uri(new Uri(instance.EndsWith('/') ? instance : instance + "/"),
                $"{tenantId}/v2.0/.well-known/openid-configuration");

            builder.AddUrlGroup(metadataUri, EntraMetadataCheck,
                HealthStatus.Unhealthy, tags: new[] { "ready" });

            builder.AddCheck<EntraStartupHealthCheck>(StartupReadyCheck, tags: new[] { "startup" });
        }
        else
        {
            // Mock-auth dev mode: skip metadata reachability check; mark startup ready immediately.
            builder.AddCheck("entra-metadata-mock",
                () => HealthCheckResult.Healthy("Development mock authentication active."),
                tags: new[] { "ready" });

            builder.AddCheck("startup-mock",
                () => HealthCheckResult.Healthy("Development startup complete."),
                tags: new[] { "startup" });
        }

        if (!string.IsNullOrWhiteSpace(keyVaultUri)
            && Uri.TryCreate(keyVaultUri, UriKind.Absolute, out _))
        {
            builder.AddCheck(KeyVaultCheck,
                () => HealthCheckResult.Healthy("Key Vault URI configured."),
                tags: new[] { "ready" });
        }

        services.AddSingleton<EntraStartupHealthCheck>();
        services.AddHttpClient(EntraStartupHealthCheck.HttpClientName);
        return services;
    }

    public static IEndpointRouteBuilder MapBusTerminalHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/healthz/live", () => Results.Ok(new
        {
            status = "Healthy",
            check = "liveness",
        })).AllowAnonymous().WithName("HealthLive");

        endpoints.MapHealthChecks("/healthz/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = HealthResponseWriter.WriteJson,
        }).AllowAnonymous().WithName("HealthReady");

        endpoints.MapHealthChecks("/healthz/startup", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("startup"),
            ResponseWriter = HealthResponseWriter.WriteJson,
        }).AllowAnonymous().WithName("HealthStartup");

        return endpoints;
    }
}

internal static class HealthResponseWriter
{
    public static System.Threading.Tasks.Task WriteJson(
        HttpContext context,
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
            }),
        };
        return context.Response.WriteAsJsonAsync(payload);
    }
}

internal sealed class EntraStartupHealthCheck : IHealthCheck
{
    public const string HttpClientName = "EntraMetadata";

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private int _completed;

    public EntraStartupHealthCheck(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _completed) == 1)
        {
            return HealthCheckResult.Healthy("Entra metadata previously fetched.");
        }

        var azureAd = _configuration.GetSection("AzureAd");
        var instance = azureAd["Instance"] ?? "https://login.microsoftonline.com/";
        var tenantId = azureAd["TenantId"];
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return HealthCheckResult.Unhealthy("AzureAd:TenantId not configured.");
        }

        var metadataUri = new Uri(new Uri(instance.EndsWith('/') ? instance : instance + "/"),
            $"{tenantId}/v2.0/.well-known/openid-configuration");

        try
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.GetAsync(metadataUri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                Interlocked.Exchange(ref _completed, 1);
                return HealthCheckResult.Healthy("Entra metadata fetched.");
            }

            return HealthCheckResult.Unhealthy(
                $"Entra metadata request returned {(int)response.StatusCode}.");
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy("Entra metadata fetch failed.", ex);
        }
        catch (TaskCanceledException ex)
        {
            return HealthCheckResult.Unhealthy("Entra metadata fetch timed out.", ex);
        }
    }
}
