using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
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
internal sealed partial class RegistryEmulatorBootstrapper : IHostedService
{
    private readonly CosmosClient _client;
    private readonly CosmosRegistryOptions _registryOptions;
    private readonly CosmosOptions _cosmosOptions;
    private readonly ILogger<RegistryEmulatorBootstrapper> _logger;

    public RegistryEmulatorBootstrapper(
        CosmosClient client,
        IOptions<CosmosRegistryOptions> registryOptions,
        IOptions<CosmosOptions> cosmosOptions,
        ILogger<RegistryEmulatorBootstrapper> logger)
    {
        _client = client;
        _registryOptions = registryOptions.Value;
        _cosmosOptions = cosmosOptions.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsEmulator())
        {
            return;
        }

        LogProvisioning(_logger, _cosmosOptions.Endpoint, _registryOptions.Database);

        var db = (await _client.CreateDatabaseIfNotExistsAsync(
            _registryOptions.Database,
            cancellationToken: cancellationToken).ConfigureAwait(false)).Database;

        // Partition keys + TTL mirror iac/modules/cosmos-registry-store/main.tf.
        // `registry-entities` uses per-item TTL (DefaultTimeToLive = -1) so the
        // tombstone-then-delete pattern (research §10) can self-expire markers.
        await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_registryOptions.EntitiesContainer, "/environment")
            {
                DefaultTimeToLive = -1,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_registryOptions.AuditContainer, "/entityId"),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_registryOptions.LeasesContainer, "/id"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool IsEmulator()
    {
        if (string.IsNullOrWhiteSpace(_cosmosOptions.Endpoint))
        {
            return false;
        }
        if (!Uri.TryCreate(_cosmosOptions.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return false;
        }
        return string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Cosmos emulator detected at {Endpoint}; provisioning database '{Database}' + registry containers.")]
    private static partial void LogProvisioning(ILogger logger, string endpoint, string database);
}
