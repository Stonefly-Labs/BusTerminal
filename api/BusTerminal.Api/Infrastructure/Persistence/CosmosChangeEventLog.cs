using System.Runtime.CompilerServices;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Lifecycle;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 004 / FR-015 / Q5. Append-only writes into the `change-events` container,
// partitioned by /resourceId. The append surfacing intentionally re-throws on
// failure — the canonical store's caller logs the diagnostic; rollback semantics
// are documented in data-model.md §Change-event log emission.
public sealed class CosmosChangeEventLog : IChangeEventLog
{
    private readonly Container _container;

    public CosmosChangeEventLog(CosmosClient client, IOptions<CosmosOptions> options)
    {
        var opts = options.Value;
        _container = client.GetContainer(opts.Database, opts.Containers.ChangeEvents);
    }

    public async Task AppendAsync(ChangeEvent evt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        await _container.CreateItemAsync(
            evt,
            new PartitionKey(evt.ResourceId.ToString()),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<ChangeEvent> QueryAsync(
        ResourceId resourceId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var definition = new QueryDefinition(
            "SELECT * FROM c WHERE c.resourceId = @id ORDER BY c.timestamp ASC")
            .WithParameter("@id", resourceId.ToString());

        using var iterator = _container.GetItemQueryIterator<ChangeEvent>(
            definition,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(resourceId.ToString()) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var item in page)
            {
                yield return item;
            }
        }
    }
}
