using BusTerminal.Api.Infrastructure.Persistence;

namespace BusTerminal.Api.Domain.Relationships;

// Spec 004 / FR-008 / T105. In-process BFS traversal over the relationship
// graph. The graph is queried hop-by-hop through ICanonicalResourceStore so the
// helper holds no Cosmos state of its own. Cycle protection via a visited-set;
// max-hop ceiling guarantees termination regardless of cycle topology (Edge
// Case "Circular relationships").
//
// The traversal is iterative and breadth-first so the hop ordering in the
// returned path mirrors graph distance — the integration test asserts that
// ordering is deterministic and the per-hop type matches the relationship type.
public sealed class RelationshipGraph
{
    private readonly ICanonicalResourceStore _store;

    public RelationshipGraph(ICanonicalResourceStore store)
    {
        _store = store;
    }

    public async Task<TraversalResult> TraverseAsync(
        ResourceId startId,
        IReadOnlyCollection<RelationshipType>? allowedTypes,
        int maxHops,
        Direction direction,
        CancellationToken cancellationToken)
    {
        if (maxHops < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHops), maxHops, "maxHops must be non-negative.");
        }

        var typeFilter = allowedTypes is null || allowedTypes.Count == 0
            ? null
            : new HashSet<RelationshipType>(allowedTypes);

        var visited = new HashSet<ResourceId> { startId };
        var hops = new List<TraversedHop>();
        var frontier = new Queue<FrontierEntry>();
        frontier.Enqueue(new FrontierEntry(startId, 0));

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current.HopDepth >= maxHops)
            {
                continue;
            }

            await foreach (var edge in _store
                .QueryRelationshipsAsync(new RelationshipQuery.ByEndpoint(current.NodeId, direction), cancellationToken)
                .ConfigureAwait(false))
            {
                if (typeFilter is not null && !typeFilter.Contains(edge.Type))
                {
                    continue;
                }

                var neighbor = ResolveNeighbor(current.NodeId, edge, direction);
                if (neighbor is null)
                {
                    continue;
                }

                // BFS spanning-tree semantic: only record a hop when the
                // neighbor is newly discovered. Back-edges to already-visited
                // nodes would otherwise noise up the path and break the
                // deterministic-ordering guarantee callers depend on.
                if (visited.Add(neighbor.Value))
                {
                    hops.Add(new TraversedHop(
                        Depth: current.HopDepth + 1,
                        From: current.NodeId,
                        To: neighbor.Value,
                        Type: edge.Type,
                        RelationshipId: edge.Id));

                    frontier.Enqueue(new FrontierEntry(neighbor.Value, current.HopDepth + 1));
                }
            }
        }

        return new TraversalResult(startId, hops, visited);
    }

    // For Outbound direction we follow source → target. For Inbound we follow
    // target → source. For Both we follow whichever endpoint is not the current
    // node; if the relationship is a self-loop (illegal in v1, but defensive)
    // the edge is skipped here so visited-set semantics remain consistent.
    private static ResourceId? ResolveNeighbor(ResourceId currentNode, Relationship edge, Direction direction)
    {
        return direction switch
        {
            Direction.Outbound => edge.SourceId == currentNode ? edge.TargetId : null,
            Direction.Inbound => edge.TargetId == currentNode ? edge.SourceId : null,
            Direction.Both => edge.SourceId == currentNode
                ? edge.TargetId
                : edge.TargetId == currentNode
                    ? edge.SourceId
                    : null,
            _ => null,
        };
    }

    private readonly record struct FrontierEntry(ResourceId NodeId, int HopDepth);
}

public sealed record TraversedHop(
    int Depth,
    ResourceId From,
    ResourceId To,
    RelationshipType Type,
    ResourceId RelationshipId);

public sealed record TraversalResult(
    ResourceId StartId,
    IReadOnlyList<TraversedHop> Hops,
    IReadOnlySet<ResourceId> Visited);
