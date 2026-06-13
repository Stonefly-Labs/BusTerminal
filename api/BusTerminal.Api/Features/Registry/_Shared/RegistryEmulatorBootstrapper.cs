using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Features.Registry.Shared;

// Provisions the registry database + containers when the API targets the
// Cosmos emulator (Endpoint host == "localhost"). IaC owns the schema in
// real Azure; this fills the gap that left the spec-006 E2E tests failing
// in CI once spec-007 unfixme'd them — the emulator boots empty and the
// runtime path doesn't auto-provision. Gated on endpoint, not
// IHostEnvironment, so a dev pointing at real Cosmos never gets write
// attempts they may not be authorized for. Idempotent (`*IfNotExistsAsync`),
// so reruns against an already-provisioned emulator are no-ops.
//
// Endpoint is sniffed via IConfiguration (not IOptions<CosmosOptions>.Value)
// so integration tests that don't configure Cosmos can still boot the host
// without tripping CosmosOptionsValidator. CosmosClient is resolved lazily
// via IServiceProvider for the same reason — eager DI would force the
// CosmosClientFactory ctor (which calls Options.Value) to run on every test
// using WebApplicationFactory<Program>.
internal sealed partial class RegistryEmulatorBootstrapper : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RegistryEmulatorBootstrapper> _logger;

    public RegistryEmulatorBootstrapper(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<RegistryEmulatorBootstrapper> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var endpoint = _configuration["Cosmos:Endpoint"];
        if (!IsEmulator(endpoint))
        {
            return;
        }

        var registryOptions = _serviceProvider.GetRequiredService<IOptions<CosmosRegistryOptions>>().Value;
        var client = _serviceProvider.GetRequiredService<CosmosClient>();

        LogProvisioning(_logger, endpoint!, registryOptions.Database);

        var db = (await client.CreateDatabaseIfNotExistsAsync(
            registryOptions.Database,
            cancellationToken: cancellationToken).ConfigureAwait(false)).Database;

        // Partition keys + TTL mirror iac/modules/cosmos-registry-store/main.tf.
        // `registry-entities` uses per-item TTL (DefaultTimeToLive = -1) so the
        // tombstone-then-delete pattern (research §10) can self-expire markers.
        await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties(registryOptions.EntitiesContainer, "/environment")
            {
                DefaultTimeToLive = -1,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties(registryOptions.AuditContainer, "/entityId"),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties(registryOptions.LeasesContainer, "/id"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool IsEmulator(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }
        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Cosmos emulator detected at {Endpoint}; provisioning database '{Database}' + registry containers.")]
    private static partial void LogProvisioning(ILogger logger, string endpoint, string database);
}
