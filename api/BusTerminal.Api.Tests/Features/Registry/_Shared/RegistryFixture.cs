using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Infrastructure.Configuration;
using BusTerminal.Api.Infrastructure.Credentials;
using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Infrastructure.Search;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusTerminal.Api.Tests.Features.Registry._Shared;

// Spec 006 / T033. Cosmos integration fixture for the registry slice. Targets
// the spec-005 dev Cosmos account; configuration comes from the test runner's
// environment so the fixture is opt-in (skipped when the env vars are absent).
// Tests prefix every entity id with a per-test GUID so parallel runs are
// partition-isolated; teardown is per-test-class via point deletes.
public sealed class RegistryFixture : IAsyncLifetime
{
    public IServiceProvider Services { get; private set; } = null!;
    public CosmosClient Client { get; private set; } = null!;

    public string Environment { get; private set; } = "test";

    public IRegistryEntityStore Store => Services.GetRequiredService<IRegistryEntityStore>();
    public IAuditEventStore AuditStore => Services.GetRequiredService<IAuditEventStore>();

    public bool IsConfigured { get; private set; }

    // Per-test GUID seed; prepended to entity ids and tag values so concurrent
    // test runs against the same dev container do not collide.
    public Guid TestRunId { get; } = Guid.NewGuid();

    public Task InitializeAsync()
    {
        var endpoint = System.Environment.GetEnvironmentVariable("BUSTERMINAL_TEST_COSMOS_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            // The fixture is opt-in — integration tests that consume it should
            // call `RequireConfigured()` and gracefully skip when no env coords
            // are provided. CI wires the env at the dev-cluster step.
            IsConfigured = false;
            return Task.CompletedTask;
        }

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cosmos:Endpoint"] = endpoint,
                ["Cosmos:Database"] = "canonical",
                ["Cosmos:Containers:Resources"] = "resources",
                ["Cosmos:Containers:ChangeEvents"] = "change-events",
                ["CosmosRegistry:Database"] = "canonical",
                ["CosmosRegistry:EntitiesContainer"] = System.Environment.GetEnvironmentVariable("BUSTERMINAL_TEST_REGISTRY_CONTAINER") ?? "registry-entities",
                ["CosmosRegistry:AuditContainer"] = System.Environment.GetEnvironmentVariable("BUSTERMINAL_TEST_AUDIT_CONTAINER") ?? "registry-audit",
                ["CosmosRegistry:LeasesContainer"] = "registry-entities-leases",
                ["AiSearch:Endpoint"] = System.Environment.GetEnvironmentVariable("BUSTERMINAL_TEST_SEARCH_ENDPOINT") ?? string.Empty,
                ["AiSearch:IndexName"] = "registry-entities-v1",
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();
        services.AddSingleton<IAzureCredentialFactory>(new AzureCredentialFactory(new TestHostEnvironment()));
        services.AddCosmosCanonicalStore(configuration);
        services.AddRegistryFeature(configuration);

        Services = services.BuildServiceProvider();
        Client = Services.GetRequiredService<CosmosClient>();
        Environment = System.Environment.GetEnvironmentVariable("BUSTERMINAL_TEST_REGISTRY_ENV") ?? "test";
        IsConfigured = true;

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }

    // Convenience for derived tests — short-circuit when the dev-Cosmos
    // coordinates are absent. xUnit 2.x has no runtime Assert.Skip; the
    // pragmatic alternative is an early return so unconfigured envs don't
    // explode the suite. CI categorises these tests `Integration` so the
    // unit tier excludes them entirely; the integration tier brings the
    // Cosmos emulator up — when neither emulator nor dev coords are present
    // the test no-ops.
    public bool ShouldRun() => IsConfigured;

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "BusTerminal.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

[CollectionDefinition("RegistryFixture")]
public sealed class RegistryFixtureCollection : ICollectionFixture<RegistryFixture>;
