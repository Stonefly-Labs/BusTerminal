// Spec 006 / T044. Indexer host wiring.
// Registers the dependencies the change-feed-triggered RegistryEntityIndexer
// needs:
//   - IndexNames (env-backed config)
//   - SearchClient (singleton; backed by ManagedIdentityCredential in prod,
//     DefaultAzureCredential in dev so az/VSCode auth keeps working)
//   - ISearchDocumentMapper → SearchDocumentMapper (singleton)
//   - IPoisonHandler → PoisonHandler (singleton)
//   - OpenTelemetry (Functions worker) with cloud role name `busterminal-indexer`
//
// The Cosmos change-feed trigger does NOT need a CosmosClient registered in
// DI — the binding reads its own connection (Cosmos__accountEndpoint +
// Cosmos__credential + Cosmos__clientId) from configuration. SearchClient is
// the only outbound SDK client we own.

using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.ResourceManager;
using Azure.Search.Documents;
using BusTerminal.Indexer.Discovery;
using BusTerminal.Indexer.Discovery.Persistence;
using BusTerminal.Indexer.Discovery.Providers;
using BusTerminal.Indexer.Discovery.Telemetry;
using BusTerminal.Indexer.Functions;
using BusTerminal.Indexer.Indexing;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Resources;

const string IndexerRoleName = "busterminal-indexer";
const string AzureClientIdKey = "AZURE_CLIENT_ID";

var builder = FunctionsApplication.CreateBuilder(args);

// Issue #118 — OpenTelemetry for Azure Functions (replaces the classic App
// Insights worker SDK). host.json sets telemetryMode=OpenTelemetry so the
// Functions host emits OTel; UseFunctionsWorkerDefaults lights up the
// worker-process pipeline (invocation spans + ILogger→OTel) and
// UseAzureMonitorExporter ships traces/logs/metrics to appi-bt-dev via
// `APPLICATIONINSIGHTS_CONNECTION_STRING`.
//
//   - ConfigureResource(AddService) sets service.name = `busterminal-indexer`,
//     which App Insights surfaces as the cloud role name — segregating the
//     indexer's telemetry from the API role (`busterminal-api`). This replaces
//     the classic CloudRoleNameInitializer.
//   - AddSource/AddMeter subscribe the discovery ActivitySource + Meter
//     (spec 009) so their spans/metrics flow through the new pipeline; the
//     exporter wires the transport but does not auto-subscribe first-party
//     sources.
//
// Unlike the API's Azure.Monitor.OpenTelemetry.AspNetCore distro (#113), the
// bare exporter does NOT register the AzureVMResourceDetector, so the indexer
// sidesteps the IMDS (169.254.169.254) probe that hangs on Container Apps.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(IndexerRoleName))
    .WithTracing(tracing => tracing.AddSource(DiscoveryActivitySource.Name))
    .WithMetrics(metrics => metrics.AddMeter(DiscoveryMeter.Name))
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter();

// Configuration-backed helper that resolves the AI Search index name + the
// Cosmos container names at startup so the trigger and the SearchClient pull
// from the same source of truth.
builder.Services.AddSingleton<IndexNames>();

// Single SearchClient instance for the process lifetime. Constructing it
// once keeps the underlying TokenCredential's in-memory token cache warm —
// per-invocation construction would re-probe the credential chain and
// re-acquire from IMDS on every change-feed batch.
builder.Services.AddSingleton(provider =>
{
    var environment = provider.GetRequiredService<IHostEnvironment>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    var names = provider.GetRequiredService<IndexNames>();

    var credential = CreateCredential(environment, configuration[AzureClientIdKey]);
    return new SearchClient(new Uri(names.SearchEndpoint), names.SearchIndex, credential);
});

builder.Services.AddSingleton<ISearchDocumentMapper, SearchDocumentMapper>();
builder.Services.AddSingleton<IPoisonHandler, PoisonHandler>();

// Spec 009 / T055 — discovery slice wiring.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DiscoveryMeter>();
builder.Services.AddSingleton<ArmClient>(sp =>
{
    var environment = sp.GetRequiredService<IHostEnvironment>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var credential = CreateCredential(environment, configuration[AzureClientIdKey]);
    return new ArmClient(credential);
});
builder.Services.AddSingleton<IEntityDiscoveryProvider, AzureServiceBusEntityDiscoveryProvider>();
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var environment = sp.GetRequiredService<IHostEnvironment>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var endpoint = configuration["Cosmos__accountEndpoint"]
        ?? configuration["COSMOS_ACCOUNT_ENDPOINT"]
        ?? throw new InvalidOperationException("Cosmos endpoint must be configured.");
    var credential = CreateCredential(environment, configuration[AzureClientIdKey]);
    return new CosmosClient(endpoint, credential);
});
builder.Services.AddSingleton<IPublishedEntityWriter>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var cosmos = sp.GetRequiredService<CosmosClient>();
    var database = configuration["COSMOS_DATABASE_NAME"] ?? "canonical";
    var containerName = configuration["COSMOS_REGISTRY_ENTITIES_CONTAINER"] ?? "registry-entities";
    var container = cosmos.GetContainer(database, containerName);
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CosmosPublishedEntityWriter>>();
    return new CosmosPublishedEntityWriter(container, logger);
});
builder.Services.AddSingleton<IDiscoveryRunUpdater>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var cosmos = sp.GetRequiredService<CosmosClient>();
    var database = configuration["COSMOS_DATABASE_NAME"] ?? "canonical";
    var runsContainer = configuration["COSMOS_DISCOVERY_RUNS_CONTAINER"] ?? "discovery-runs";
    var container = cosmos.GetContainer(database, runsContainer);
    return new CosmosDiscoveryRunUpdater(container);
});
builder.Services.AddSingleton<INamespaceContextResolver>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var cosmos = sp.GetRequiredService<CosmosClient>();
    var database = configuration["COSMOS_DATABASE_NAME"] ?? "canonical";
    var containerName = configuration["COSMOS_REGISTRY_ENTITIES_CONTAINER"] ?? "registry-entities";
    var container = cosmos.GetContainer(database, containerName);
    return new CosmosNamespaceContextResolver(container);
});
builder.Services.AddSingleton<EntityDiscoveryOrchestrator>();

builder.Build().Run();

// Mirrors BusTerminal.Api/Infrastructure/Credentials/AzureCredentialFactory.
// Pinned to ManagedIdentityCredential in non-Development when the workload
// UAMI client id is wired — skips the DefaultAzureCredential chain probe
// and keeps the indexer's identity selection consistent with the API.
static TokenCredential CreateCredential(IHostEnvironment environment, string? userAssignedClientId)
{
    if (environment.IsDevelopment())
    {
        return new DefaultAzureCredential();
    }
    if (!string.IsNullOrWhiteSpace(userAssignedClientId))
    {
        return new ManagedIdentityCredential(
            ManagedIdentityId.FromUserAssignedClientId(userAssignedClientId));
    }
    return new DefaultAzureCredential();
}
