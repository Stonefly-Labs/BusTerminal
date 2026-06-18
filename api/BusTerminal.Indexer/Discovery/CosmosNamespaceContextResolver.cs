using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Indexer.Discovery;

// Spec 009 / T053 helper. Reads the registered namespace document from the
// `registry-entities` Cosmos container to derive the ARM coordinates the
// provider needs. Each registry namespace carries `subscriptionId`,
// `resourceGroup`, `fullyQualifiedName` (== namespace name), and
// `environment` per spec 008.
public sealed class CosmosNamespaceContextResolver : INamespaceContextResolver
{
    private readonly Container _container;

    public CosmosNamespaceContextResolver(Container container)
    {
        _container = container;
    }

    public async Task<NamespaceDiscoveryContext> ResolveAsync(string namespaceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);

        // The spec-008 namespace document uses /environment as PK. The Worker
        // does not know the environment up-front — we do a cross-partition
        // lookup by id (one document at most). For production scale this
        // single-shot read is acceptable; future optimization could cache.
        var query = new QueryDefinition(
                "SELECT TOP 1 c.id, c.environment, c.fullyQualifiedName, c.subscriptionId, c.resourceGroup, c.namespaceName " +
                "FROM c WHERE c.id = @id")
            .WithParameter("@id", namespaceId);
        var options = new QueryRequestOptions { MaxItemCount = 1 };
        using var iterator = _container.GetItemQueryIterator<NamespaceLookupDoc>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var doc in page)
            {
                var nameOrFqn = doc.NamespaceName
                    ?? doc.FullyQualifiedName?.Split('.', 2)[0]
                    ?? throw new InvalidOperationException($"Namespace {namespaceId} is missing a name.");
                return new NamespaceDiscoveryContext(
                    AzureSubscriptionId: doc.SubscriptionId ?? throw new InvalidOperationException($"Namespace {namespaceId} is missing subscriptionId."),
                    ResourceGroup: doc.ResourceGroup ?? throw new InvalidOperationException($"Namespace {namespaceId} is missing resourceGroup."),
                    NamespaceName: nameOrFqn,
                    Environment: doc.Environment ?? "dev");
            }
        }
        throw new InvalidOperationException($"Namespace {namespaceId} not found.");
    }

    private sealed record NamespaceLookupDoc(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("environment")] string? Environment,
        [property: JsonPropertyName("fullyQualifiedName")] string? FullyQualifiedName,
        [property: JsonPropertyName("subscriptionId")] string? SubscriptionId,
        [property: JsonPropertyName("resourceGroup")] string? ResourceGroup,
        [property: JsonPropertyName("namespaceName")] string? NamespaceName);
}
