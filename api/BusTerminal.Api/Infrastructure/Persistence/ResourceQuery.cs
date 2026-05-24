using BusTerminal.Api.Domain;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Discriminated query shape consumed by ICanonicalResourceStore.QueryAsync.
// Each variant carries the parameters needed to build a Cosmos SQL query;
// rendering happens inside CosmosCanonicalResourceStore.
public abstract record ResourceQuery
{
    public sealed record All(string? ResourceTypeDiscriminator = null, bool IncludeDeleted = false) : ResourceQuery;

    public sealed record OwnedByTeam(ResourceId TeamId, bool IncludeDeleted = false) : ResourceQuery;

    public sealed record InEnvironment(EnvironmentClassification Environment, bool IncludeDeleted = false) : ResourceQuery;

    public sealed record ByNamespacePath(NamespacePath Path, bool IncludeDeleted = false) : ResourceQuery;
}
