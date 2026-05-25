using BusTerminal.Api.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BusTerminal.Tools.LoadFixtures.Commands;

// T082 — wipe both canonical containers. Used by quickstart Path B § B.4 + Smoke 7.
// Uses delete-by-partition-key for the bulk path (much cheaper than per-document deletes).
internal static class TruncateCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var client = services.GetRequiredService<CosmosClient>();
        var options = services.GetRequiredService<IOptions<CosmosOptions>>().Value;
        var database = client.GetDatabase(options.Database);

        var resourcesDeleted = await TruncateContainerAsync(
            database.GetContainer(options.Containers.Resources),
            cancellationToken).ConfigureAwait(false);

        var eventsDeleted = await TruncateContainerAsync(
            database.GetContainer(options.Containers.ChangeEvents),
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Truncated. Removed {resourcesDeleted} documents from resources, {eventsDeleted} from change-events.");
        return 0;
    }

    private static async Task<int> TruncateContainerAsync(Container container, CancellationToken cancellationToken)
    {
        // Cosmos has no "truncate" — iterate the container's partition keys and
        // delete each document. For dev / emulator volumes (< 1000 docs) this is
        // fine; a production-scale truncate is out of scope (the spec defers
        // retention/TTL design entirely).
        //
        // Idempotency: silently swallow per-document 404s. Cosmos query results
        // can race with concurrent deletes (test fixture teardown, prior
        // truncate that partially failed) and "doc is already gone" is exactly
        // the state truncate wants to reach.
        var deleted = 0;
        var idQueryDefinition = new QueryDefinition("SELECT c.id, c[\"resourceType\"] AS pk1, c.resourceId AS pk2 FROM c");
        using var iterator = container.GetItemQueryIterator<DocumentRef>(idQueryDefinition);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var doc in page)
            {
                var partitionKeyValue = doc.Pk1 ?? doc.Pk2 ?? throw new InvalidOperationException($"Document {doc.Id} has no partition-key value (neither resourceType nor resourceId).");
                try
                {
                    await container.DeleteItemAsync<object>(
                        doc.Id,
                        new PartitionKey(partitionKeyValue),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    deleted++;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Already gone — count it as a successful truncate of that id.
                    deleted++;
                }
            }
        }

        return deleted;
    }

    private sealed record DocumentRef(string Id, string? Pk1, string? Pk2);
}
