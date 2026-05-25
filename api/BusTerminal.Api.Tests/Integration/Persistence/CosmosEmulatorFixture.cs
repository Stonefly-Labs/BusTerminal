using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Infrastructure.Configuration;
using BusTerminal.Api.Infrastructure.Credentials;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T056 — xUnit collection fixture for tests that need a live Cosmos
// connection. Targets the linux-emulator service from the repo's
// docker-compose.yml. Validates the emulator is reachable, creates the
// canonical database + containers if missing, exposes a configured
// ICanonicalResourceStore / IChangeEventLog to derived test classes.
//
// Container-truncation between test classes is the responsibility of each
// derived test (deleting items is cheap on the small fixture sets); this fixture
// only handles bring-up + tear-down of the database itself.
public sealed class CosmosEmulatorFixture : IAsyncLifetime
{
    private const string EmulatorEndpoint = "https://localhost:8081";

    // The vnext-preview emulator exposes a dedicated plain-HTTP readiness probe
    // on port 8080 (`/ready`). The previous probe URL (`_explorer/emulator.pem`
    // on 8081) belongs to the legacy Windows emulator and isn't served by the
    // vnext gateway — it returned HTTP 400 and made the fixture think the
    // emulator was down even when the SDK could connect fine.
    private const string EmulatorReadinessProbe = "http://localhost:8080/ready";

    private const string EmulatorKey =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseName = "busterminal-canonical";
    private const string ResourcesContainer = "resources";
    private const string ChangeEventsContainer = "change-events";

    public CosmosClient Client { get; private set; } = null!;
    public Database Database { get; private set; } = null!;
    public Container ResourcesCosmosContainer { get; private set; } = null!;
    public Container ChangeEventsCosmosContainer { get; private set; } = null!;
    public IServiceProvider Services { get; private set; } = null!;

    public ICanonicalResourceStore Store => Services.GetRequiredService<ICanonicalResourceStore>();

    public IChangeEventLog ChangeEventLog => Services.GetRequiredService<IChangeEventLog>();

    public JsonResourceSerializer Serializer => Services.GetRequiredService<JsonResourceSerializer>();

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cosmos:Endpoint"] = EmulatorEndpoint,
                ["Cosmos:Database"] = DatabaseName,
                ["Cosmos:Containers:Resources"] = ResourcesContainer,
                ["Cosmos:Containers:ChangeEvents"] = ChangeEventsContainer,
                ["Cosmos:LocalEmulatorKey"] = EmulatorKey,
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();
        services.AddSingleton<IAzureCredentialFactory>(new AzureCredentialFactory(new TestHostEnvironment()));

        // Production DI registration — picks up registry population + the
        // UnknownResource factory automatically. Keeps the fixture in lockstep
        // with Program.cs wiring; any divergence is a bug, not a fixture quirk.
        services.AddCosmosCanonicalStore(configuration);

        Services = services.BuildServiceProvider();
        Client = Services.GetRequiredService<CosmosClient>();

        await EnsureEmulatorReachableAsync().ConfigureAwait(false);

        var dbResponse = await Client.CreateDatabaseIfNotExistsAsync(DatabaseName).ConfigureAwait(false);
        Database = dbResponse.Database;

        ResourcesCosmosContainer = (await Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ResourcesContainer, "/resourceType")).ConfigureAwait(false)).Container;

        ChangeEventsCosmosContainer = (await Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ChangeEventsContainer, "/resourceId")).ConfigureAwait(false)).Container;
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }

    private static async Task EnsureEmulatorReachableAsync()
    {
        // Use the dedicated readiness probe (plain HTTP on port 8080) per the
        // current vnext-preview docs — no TLS handshake needed for the probe.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            var response = await http.GetAsync(new Uri(EmulatorReadinessProbe)).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Cosmos emulator readiness probe at {EmulatorReadinessProbe} responded with HTTP {(int)response.StatusCode}. " +
                    "Start the emulator via `docker compose up cosmos-emulator` before running integration tests.");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Cosmos emulator not reachable at {EmulatorReadinessProbe}. Start it via " +
                "`docker compose up cosmos-emulator` before running integration tests.",
                ex);
        }
    }

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "BusTerminal.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

[CollectionDefinition("CosmosEmulator")]
public sealed class CosmosEmulatorCollection : ICollectionFixture<CosmosEmulatorFixture>;
