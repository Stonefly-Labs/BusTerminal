// Spec 006 / T044. Indexer host wiring.
// Registers the dependencies the change-feed-triggered RegistryEntityIndexer
// needs:
//   - IndexNames (env-backed config)
//   - SearchClient (singleton; backed by ManagedIdentityCredential in prod,
//     DefaultAzureCredential in dev so az/VSCode auth keeps working)
//   - ISearchDocumentMapper → SearchDocumentMapper (singleton)
//   - IPoisonHandler → PoisonHandler (singleton)
//   - Application Insights with cloud role name `busterminal-indexer`
//
// The Cosmos change-feed trigger does NOT need a CosmosClient registered in
// DI — the binding reads its own connection (Cosmos__accountEndpoint +
// Cosmos__credential + Cosmos__clientId) from configuration. SearchClient is
// the only outbound SDK client we own.

using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents;
using BusTerminal.Indexer.Functions;
using BusTerminal.Indexer.Indexing;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

const string IndexerRoleName = "busterminal-indexer";
const string AzureClientIdKey = "AZURE_CLIENT_ID";

var builder = FunctionsApplication.CreateBuilder(args);

// Application Insights + OTel-style cloud role name. The Functions worker
// extension package wires the App Insights TelemetryClient against the
// connection string in `APPLICATIONINSIGHTS_CONNECTION_STRING`; the role-name
// initializer below stamps every item with `busterminal-indexer` so traces
// segregate from the API role (`busterminal-api`).
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();
builder.Services.AddSingleton<ITelemetryInitializer>(_ => new CloudRoleNameInitializer(IndexerRoleName));

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

internal sealed class CloudRoleNameInitializer : ITelemetryInitializer
{
    private readonly string _roleName;

    public CloudRoleNameInitializer(string roleName)
    {
        _roleName = roleName;
    }

    public void Initialize(ITelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
        {
            telemetry.Context.Cloud.RoleName = _roleName;
        }
    }
}
