using BusTerminal.Api.Domain;
using BusTerminal.Api.Infrastructure.Configuration;
using BusTerminal.Api.Infrastructure.Credentials;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusTerminal.Tools.LoadFixtures;

// Spec 004 — small DI host so verb handlers can resolve ICanonicalResourceStore,
// CosmosClient, etc. via the production AddCosmosCanonicalStore wiring.
internal static class ServiceHost
{
    // Stable principal stamped onto every audit/change-event the CLI emits.
    public static readonly PrincipalReference Actor =
        new SystemPrincipalReference("load-fixtures");

    public const string SourceSystem = "tools/load-fixtures";

    public static ServiceProvider Build(CliOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException("--endpoint is required.");
        }

        var configSettings = new Dictionary<string, string?>
        {
            ["Cosmos:Endpoint"] = options.Endpoint,
            ["Cosmos:Database"] = "busterminal-canonical",
            ["Cosmos:Containers:Resources"] = "resources",
            ["Cosmos:Containers:ChangeEvents"] = "change-events",
        };

        if (string.Equals(options.Auth, CliOptions.AuthEmulatorKey, StringComparison.Ordinal))
        {
            // Quickstart documents this key as a well-known public emulator key.
            configSettings["Cosmos:LocalEmulatorKey"] =
                "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configSettings)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
                o.IncludeScopes = false;
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<IHostEnvironment>(new ConsoleHostEnvironment(
            string.Equals(options.Auth, CliOptions.AuthEmulatorKey, StringComparison.Ordinal)
                ? Environments.Development
                : Environments.Production));
        services.AddSingleton<IAzureCredentialFactory, AzureCredentialFactory>();

        services.AddCosmosCanonicalStore(configuration);

        return services.BuildServiceProvider();
    }

    private sealed class ConsoleHostEnvironment : IHostEnvironment
    {
        public ConsoleHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "busterminal-load-fixtures";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
