using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Infrastructure.Credentials;
using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

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
        services.AddSingleton<Func<string, JsonElement, Resource>>(_ =>
            (discriminator, raw) => throw new InvalidOperationException(
                $"UnknownResource factory not wired in CosmosEmulatorFixture. Discriminator: {discriminator}."));
        services.AddSingleton<ResourceTypeRegistry>();
        services.AddSingleton<ExtensionsJsonConverter>();
        services.AddSingleton<ResourceJsonConverter>();
        services.AddSingleton<JsonResourceSerializer>();
        services.AddSingleton<IResourceSerializer>(sp => sp.GetRequiredService<JsonResourceSerializer>());
        services.AddSingleton<CosmosStjSerializer>();
        services.AddOptions<CosmosOptions>().Bind(configuration.GetSection(CosmosOptions.SectionName));
        services.AddSingleton<CosmosClientFactory>();
        services.AddSingleton(sp => sp.GetRequiredService<CosmosClientFactory>().Create());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IChangeEventLog, CosmosChangeEventLog>();
        services.AddScoped<ICanonicalResourceStore, CosmosCanonicalResourceStore>();

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
        // The emulator's TLS cert is self-signed; accept it explicitly for this
        // reachability check. Mirrors CosmosClientFactory's HttpClientHandler.
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            var response = await http.GetAsync(new Uri($"{EmulatorEndpoint}/_explorer/emulator.pem")).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Cosmos emulator at {EmulatorEndpoint} responded with HTTP {(int)response.StatusCode}. " +
                    "Start the emulator via `docker compose up cosmos-emulator` before running integration tests.");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Cosmos emulator not reachable at {EmulatorEndpoint}. Start it via " +
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
