using System.Net;
using Azure.ResourceManager;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Namespaces.Validation.Checks;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Infrastructure.Identity;
using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Infrastructure.ServiceBus;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BusTerminal.Api.Tests.Features.Namespaces;

// Spec 008 / T061–T064. WebApplicationFactory tailored to the namespace
// endpoints. Reuses the spec-006 in-memory stores + adds stubs for the
// namespace-specific ports (validation run store, ARM probe, Graph picker,
// workload identity provider, ARM subscription tenant resolver).
public sealed class NamespacesContractFactory : WebApplicationFactory<Program>
{
    public InMemoryRegistryEntityStore EntityStore { get; } = new();
    public InMemoryAuditEventStore AuditStore { get; } = new();
    public InMemoryNamespaceValidationRunStore RunStore { get; } = new();
    public StubArmProbe ArmProbe { get; } = new();
    public StubGraphPicker GraphPicker { get; } = new();
    public StubArmSubscriptionTenantResolver TenantResolver { get; } = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    public static Guid WorkloadPrincipalId { get; } = Guid.Parse("99999999-9999-9999-9999-999999999999");

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
        builder.UseSetting("CosmosRegistry:ValidationRunsContainer", "namespace-validation-runs");
        builder.UseSetting("AiSearch:Endpoint", "https://example-search.search.windows.net");
        builder.UseSetting("AiSearch:IndexName", "registry-entities-v1");
        builder.UseSetting(WorkloadIdentityProvider.ConfigurationKey, WorkloadPrincipalId.ToString("D"));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRegistryEntityStore>();
            services.RemoveAll<IAuditEventStore>();
            services.AddSingleton<IRegistryEntityStore>(EntityStore);
            services.AddSingleton<IAuditEventStore>(AuditStore);

            services.RemoveAll<CosmosClient>();
            services.AddSingleton(_ => CreateNullCosmosClient());
            services.RemoveAll<ISearchClient>();
            services.AddSingleton<ISearchClient>(new FakeSearchClient());

            services.RemoveAll<INamespaceValidationRunStore>();
            services.AddSingleton<INamespaceValidationRunStore>(RunStore);

            services.RemoveAll<IArmNamespaceProbe>();
            services.AddSingleton<IArmNamespaceProbe>(ArmProbe);

            services.RemoveAll<IGraphPrincipalPicker>();
            services.AddSingleton<IGraphPrincipalPicker>(GraphPicker);

            services.RemoveAll<IArmSubscriptionTenantResolver>();
            services.AddSingleton<IArmSubscriptionTenantResolver>(TenantResolver);

            services.RemoveAll<ArmClient>();
            services.AddSingleton(_ => new ArmClient(new Azure.Identity.DefaultAzureCredential()));
        });
    }

    private static CosmosClient CreateNullCosmosClient()
    {
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
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}

public sealed class InMemoryNamespaceValidationRunStore : INamespaceValidationRunStore
{
    private readonly List<ValidationRun> _items = new();

    public IReadOnlyList<ValidationRun> All() => _items.ToArray();

    public Task AppendAsync(ValidationRun run, CancellationToken cancellationToken)
    {
        _items.Add(run);
        return Task.CompletedTask;
    }

    public Task<ValidationRunPage> ListForNamespaceAsync(Guid namespaceId, int limit, string? continuationToken, CancellationToken cancellationToken)
        => Task.FromResult(new ValidationRunPage(
            _items.Where(r => r.NamespaceId == namespaceId).Take(limit).ToArray(),
            ContinuationToken: null));

    public Task<ValidationRun?> GetAsync(Guid namespaceId, Guid runId, CancellationToken cancellationToken)
        => Task.FromResult(_items.FirstOrDefault(r => r.NamespaceId == namespaceId && r.Id == runId));
}

public sealed class StubArmProbe : IArmNamespaceProbe
{
    public ArmResourceSnapshot? Snapshot { get; set; } =
        new("eastus2", "rg-payments-prod", Guid.Parse("11111111-2222-3333-4444-555555555555"), DateTimeOffset.UtcNow);

    public ValidationCheckOutcome ExistenceOutcome { get; set; } = ValidationCheckOutcome.Pass;
    public ValidationCheckOutcome AccessibilityOutcome { get; set; } = ValidationCheckOutcome.Pass;
    public ValidationCheckOutcome RequiredPermissionsOutcome { get; set; } = ValidationCheckOutcome.Pass;
    public ValidationCheckOutcome IdentityAuthorizationOutcome { get; set; } = ValidationCheckOutcome.Pass;
    public ValidationCheckOutcome ApiReachabilityOutcome { get; set; } = ValidationCheckOutcome.Pass;

    public Task<ArmProbeResult> ProbeExistenceAsync(NamespaceArmId armId, CancellationToken cancellationToken)
        => Task.FromResult(new ArmProbeResult(ExistenceOutcome, Map(ExistenceOutcome), "OK", Snapshot: Snapshot));
    public Task<ArmProbeResult> ProbeAccessibilityAsync(NamespaceArmId armId, CancellationToken cancellationToken)
        => Task.FromResult(new ArmProbeResult(AccessibilityOutcome, Map(AccessibilityOutcome), "OK"));
    public Task<ArmProbeResult> ProbeRequiredPermissionsAsync(NamespaceArmId armId, CancellationToken cancellationToken)
        => Task.FromResult(new ArmProbeResult(RequiredPermissionsOutcome, Map(RequiredPermissionsOutcome), "OK"));
    public Task<ArmProbeResult> ProbeIdentityAuthorizationAsync(NamespaceArmId armId, CancellationToken cancellationToken)
        => Task.FromResult(new ArmProbeResult(IdentityAuthorizationOutcome, Map(IdentityAuthorizationOutcome), "OK"));
    public Task<ArmProbeResult> ProbeApiReachabilityAsync(NamespaceArmId armId, CancellationToken cancellationToken)
        => Task.FromResult(new ArmProbeResult(ApiReachabilityOutcome, Map(ApiReachabilityOutcome), "OK"));

    private static ValidationFailureCategory Map(ValidationCheckOutcome outcome)
        => outcome == ValidationCheckOutcome.Pass ? ValidationFailureCategory.Ok : ValidationFailureCategory.Unknown;
}

public sealed class StubGraphPicker : IGraphPrincipalPicker
{
    public List<PrincipalPickerItem> Items { get; } = new();

    public Task<IReadOnlyList<PrincipalPickerItem>> SearchAsync(
        string query,
        int top,
        bool includeGroups,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PrincipalPickerItem>>(Items
            .Where(i => includeGroups || i.PrincipalType != PrincipalType.Group)
            .Take(top)
            .ToArray());
}

public sealed class StubArmSubscriptionTenantResolver : IArmSubscriptionTenantResolver
{
    private readonly Guid _tenantId;

    public StubArmSubscriptionTenantResolver(Guid tenantId) => _tenantId = tenantId;

    public Task<TenantResolution> ResolveTenantIdAsync(Guid subscriptionId, CancellationToken cancellationToken)
        => Task.FromResult(new TenantResolution(_tenantId, TenantResolutionOutcome.Resolved));
}
