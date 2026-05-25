using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Relationships;

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

// Relationship-specific query shapes. Spec 004 / T104.
public abstract record RelationshipQuery
{
    public sealed record ByEndpoint(ResourceId EndpointId, Direction Direction = Direction.Both, bool IncludeDeleted = false) : RelationshipQuery;

    public sealed record ByType(RelationshipType Type, bool IncludeDeleted = false) : RelationshipQuery;

    public sealed record All(bool IncludeDeleted = false) : RelationshipQuery;
}
