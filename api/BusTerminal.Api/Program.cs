using BusTerminal.Api.Authorization;
using BusTerminal.Api.Features.Health;
using BusTerminal.Api.Features.Identity;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Features.RoleProbes;
using BusTerminal.Api.Infrastructure.Authentication;
using BusTerminal.Api.Infrastructure.Configuration;
using BusTerminal.Api.Infrastructure.Credentials;
using BusTerminal.Api.Infrastructure.Graph;
using BusTerminal.Api.Infrastructure.Observability;
using Microsoft.AspNetCore.Authorization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var azureCredentialFactory = new AzureCredentialFactory(builder.Environment);
builder.Configuration.AddBusTerminalKeyVault(builder.Environment, azureCredentialFactory);

builder.AddBusTerminalTelemetry();

// Default listen port is 8080 — overridable via BUSTERMINAL_API_PORT for
// local-dev scenarios where 8080 is taken (e.g. the Cosmos emulator's
// readiness probe also runs there).
var apiPort = int.TryParse(
    Environment.GetEnvironmentVariable("BUSTERMINAL_API_PORT"),
    out var configuredPort) && configuredPort > 0
        ? configuredPort
        : 8080;
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(apiPort);
});

builder.Services.AddBusTerminalAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddBusTerminalHealthChecks(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPlatformPrincipalAccessor, PrincipalAccessor>();
builder.Services.AddSingleton<IAzureCredentialFactory>(azureCredentialFactory);
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, BusTerminalAuthorizationMiddlewareResultHandler>();
builder.Services.AddBusTerminalGraphClient();

// Spec 004 — canonical resource store + change-event log + validation engine.
builder.Services.AddCosmosCanonicalStore(builder.Configuration);

// Spec 006 — registry slice. Persistence + audit + search + helpers.
// US1 endpoints wired via `app.MapRegistryEndpoints()` below.
builder.Services.AddRegistryFeature(builder.Configuration);

// Spec 008 — namespace onboarding slice. ARM probe + Graph picker +
// workload identity provider + ValidationRun store + validators + the
// CanAdministerNamespaces policy. Endpoints attach to the group via
// `app.MapNamespaceEndpoints()` below.
builder.Services.AddNamespaceOnboardingFeature(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddRouting();

var app = builder.Build();

// CORS for cross-origin browser fetches. Local dev has the Next.js dev
// server at :3000 and the API at :8080; deployed envs run frontend + API
// as separate Container Apps with distinct FQDNs (e.g.
// `ca-bt-{env}-web.*.azurecontainerapps.io` vs `ca-bt-{env}-api.*...`), so
// every browser-originated request to the API is cross-origin — preflight
// included.
//
// Allowlist comes from config (`Cors:AllowedOrigins`, an array; IaC injects
// `Cors__AllowedOrigins__0 = <frontend fqdn>` per env). Built-in localhost
// entries are always allowed so local-dev flows work even when config
// supplies a deployed FQDN — Origin is browser-controlled and not
// spoofable, so always-localhost has no production blast radius.
//
// Raw middleware (not framework `UseCors()`) because minimal-API +
// endpoint-routing's 405 dispatch short-circuits an OPTIONS preflight to a
// GET-only route before the framework CORS middleware can intercept it.
// This runs strictly first in the pipeline and emits the preflight
// response unconditionally.
var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "http://localhost:3000",
    "http://127.0.0.1:3000",
};
var configuredOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();
if (configuredOrigins is not null)
{
    foreach (var configured in configuredOrigins)
    {
        if (string.IsNullOrWhiteSpace(configured)) continue;
        allowedOrigins.Add(configured.TrimEnd('/'));
    }
}
app.Use(async (ctx, next) =>
{
    var origin = ctx.Request.Headers.Origin.ToString();
    if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin))
    {
        ctx.Response.Headers["Access-Control-Allow-Origin"] = origin;
        ctx.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        ctx.Response.Headers.Vary = "Origin";
        if (HttpMethods.IsOptions(ctx.Request.Method))
        {
            var reqHeaders = ctx.Request.Headers["Access-Control-Request-Headers"].ToString();
            ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, PATCH, OPTIONS";
            ctx.Response.Headers["Access-Control-Allow-Headers"] =
                string.IsNullOrEmpty(reqHeaders)
                    ? "Authorization, Content-Type, traceparent, X-Mock-Roles, X-Mock-Caller-Type"
                    : reqHeaders;
            ctx.Response.Headers["Access-Control-Max-Age"] = "600";
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }
    }
    await next();
});

app.UseSerilogRequestLogging();

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    // Reserved for future Swagger UI / API explorer wiring.
}

app.UseAuthentication();
app.UseAuthorization();

app.MapBusTerminalHealthEndpoints();
app.MapWhoAmIEndpoint();
app.MapRoleProbeEndpoints();
app.MapRegistryEndpoints();
app.MapNamespaceEndpoints();

try
{
    Log.Information("Starting BusTerminal API on port {Port} ({Environment})", apiPort, app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
