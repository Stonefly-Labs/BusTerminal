using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// T079 — create the canonical database + both containers. Idempotent (uses
// CreateXxxIfNotExistsAsync). Mirrors the partition-key choices the IaC modules
// provision for production / dev.
internal static class CreateDatabaseCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var client = services.GetRequiredService<CosmosClient>();
        var options = services.GetRequiredService<IOptions<CosmosOptions>>().Value;

        var database = (await client.CreateDatabaseIfNotExistsAsync(
            options.Database,
            cancellationToken: cancellationToken).ConfigureAwait(false)).Database;

        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(options.Containers.Resources, "/resourceType"),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(options.Containers.ChangeEvents, "/resourceId"),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Database '{options.Database}' and containers '{options.Containers.Resources}', '{options.Containers.ChangeEvents}' are ready.");
        return 0;
    }
}
