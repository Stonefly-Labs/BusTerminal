using Microsoft.Extensions.Configuration;

namespace BusTerminal.Indexer.Indexing;

// Spec 006 / T047 / contracts/indexer-events.md §1. Single source of truth for
// the search-index name + the Cosmos container names the trigger binds to.
// All values come from environment variables so the same image works across
// dev/test/prod without code changes.
public sealed class IndexNames
{
    public IndexNames(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        SearchIndex = configuration["AI_SEARCH_INDEX_NAME"]
            ?? throw new InvalidOperationException("AI_SEARCH_INDEX_NAME must be set.");
        SearchEndpoint = configuration["AI_SEARCH_ENDPOINT"]
            ?? throw new InvalidOperationException("AI_SEARCH_ENDPOINT must be set.");
        CosmosDatabase = configuration["COSMOS_DATABASE_NAME"]
            ?? throw new InvalidOperationException("COSMOS_DATABASE_NAME must be set.");
        EntitiesContainer = configuration["COSMOS_REGISTRY_ENTITIES_CONTAINER"]
            ?? throw new InvalidOperationException("COSMOS_REGISTRY_ENTITIES_CONTAINER must be set.");
        LeasesContainer = configuration["COSMOS_REGISTRY_LEASES_CONTAINER"]
            ?? throw new InvalidOperationException("COSMOS_REGISTRY_LEASES_CONTAINER must be set.");
    }

    public string SearchIndex { get; }
    public string SearchEndpoint { get; }
    public string CosmosDatabase { get; }
    public string EntitiesContainer { get; }
    public string LeasesContainer { get; }
}
