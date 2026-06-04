using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Infrastructure.Search;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BusTerminal.Api.Tests.Features.Registry.Fakes;

// Spec 006 / Phase 3 US1. WebApplicationFactory that replaces the live Cosmos
// + AI Search dependencies with in-memory fakes so the registry contract
// tests can run without external infrastructure. Authentication uses the
// existing MockAuthenticationHandler (Development env).
public sealed class RegistryContractFactory : WebApplicationFactory<Program>
{
    public InMemoryRegistryEntityStore EntityStore { get; } = new();
    public InMemoryAuditEventStore AuditStore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Development);
        builder.UseSetting("AzureAd:TenantId", "development");
        builder.UseSetting("AzureAd:Instance", "https://login.microsoftonline.com/");
        builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000000");
        builder.UseSetting("AzureAd:Audience", "api://busterminal-dev");
        builder.UseSetting("Cosmos:Endpoint", "https://example-cosmos.documents.azure.com:443/");
        builder.UseSetting("Cosmos:Database", "canonical");
        builder.UseSetting("Cosmos:Containers:Resources", "resources");
        builder.UseSetting("Cosmos:Containers:ChangeEvents", "change-events");
        builder.UseSetting("CosmosRegistry:Database", "canonical");
        builder.UseSetting("CosmosRegistry:EntitiesContainer", "registry-entities");
        builder.UseSetting("CosmosRegistry:AuditContainer", "registry-audit");
        builder.UseSetting("CosmosRegistry:LeasesContainer", "registry-entities-leases");
        builder.UseSetting("AiSearch:Endpoint", "https://example-search.search.windows.net");
        builder.UseSetting("AiSearch:IndexName", "registry-entities-v1");

        builder.ConfigureServices(services =>
        {
            // Swap the real registry stores for the in-memory fakes. The same
            // singleton instances are reused across requests so tests can
            // assert on the audit-store state after a CRUD interaction.
            services.RemoveAll<IRegistryEntityStore>();
            services.RemoveAll<IAuditEventStore>();
            services.AddSingleton<IRegistryEntityStore>(EntityStore);
            services.AddSingleton<IAuditEventStore>(AuditStore);

            // The Cosmos canonical store + AI Search client both attempt to
            // resolve managed identity at startup; replace with safe stubs
            // since the contract tests don't exercise those code paths.
            services.RemoveAll<CosmosClient>();
            services.AddSingleton(_ => CreateNullCosmosClient());
            services.RemoveAll<ISearchClient>();
            services.AddSingleton<ISearchClient, NullSearchClient>();
        });
    }

    private static CosmosClient CreateNullCosmosClient()
    {
        // Cosmos SDK doesn't ship a null client; constructing one against a
        // throwaway emulator-shaped key keeps DI happy without hitting the
        // network. The registry endpoints never call methods on it because
        // we substituted IRegistryEntityStore + IAuditEventStore above.
        var options = new CosmosClientOptions
        {
            HttpClientFactory = () => new HttpClient(new SilentHttpMessageHandler()),
            ConnectionMode = ConnectionMode.Gateway,
        };
        return new CosmosClient(
            "https://example-cosmos.documents.azure.com:443/",
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            options);
    }

    private sealed class SilentHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
        }
    }

    private sealed class NullSearchClient : ISearchClient
    {
        public Task<RegistrySearchResults> SearchAsync(RegistrySearchRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new RegistrySearchResults(Array.Empty<RegistrySearchHit>(), 0));

        public Task<IReadOnlyList<RegistrySuggestion>> SuggestAsync(
            string partialText,
            int top,
            string? environmentFilter,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<RegistrySuggestion>>(Array.Empty<RegistrySuggestion>());
    }
}
