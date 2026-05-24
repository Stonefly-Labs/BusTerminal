using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Infrastructure.Configuration;
using BusTerminal.Api.Infrastructure.Credentials;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Api.Tests.Unit.Serialization;

// Shared serializer instance built through the production DI graph so the
// converter list and naming policy match exactly what runs in Program.cs.
internal sealed class SerializerFixture : IDisposable
{
    public SerializerFixture()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cosmos:Endpoint"] = "https://localhost:8081",
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IAzureCredentialFactory>(
            new AzureCredentialFactory(new SerializerHostEnv()));
        services.AddLogging();
        services.AddCosmosCanonicalStore(configuration);

        Provider = services.BuildServiceProvider();
        Serializer = Provider.GetRequiredService<JsonResourceSerializer>();
    }

    public ServiceProvider Provider { get; }

    public JsonResourceSerializer Serializer { get; }

    public void Dispose() => Provider.Dispose();

    private sealed class SerializerHostEnv : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
