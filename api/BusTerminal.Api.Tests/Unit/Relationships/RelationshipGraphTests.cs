using System.Runtime.CompilerServices;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Relationships;
using BusTerminal.Api.Infrastructure.Persistence;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Relationships;

// Spec 004 / T111. Pure-unit tests of the BFS traversal logic — backed by an
// in-memory store stub so the test is deterministic and emulator-free.
//
// Covers: BFS termination on a finite graph, cycle protection on a cyclic
// graph, direction enforcement (Outbound, Inbound, Both), max-hop respect,
// and type-filtered traversal.
public sealed class RelationshipGraphTests
{
    private static readonly ResourceId A = ResourceId.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly ResourceId B = ResourceId.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly ResourceId C = ResourceId.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly ResourceId D = ResourceId.Parse("aaaaaaaa-0000-0000-0000-000000000004");

    [Fact]
    public async Task BFS_terminates_on_finite_acyclic_graph()
    {
        var store = new InMemoryRelationshipStore(
            Edge(A, B, RelationshipType.PublishesTo),
            Edge(B, C, RelationshipType.ConsumedBy),
            Edge(C, D, RelationshipType.PartOfFlow));

        var graph = new RelationshipGraph(store);
        var result = await graph.TraverseAsync(A, allowedTypes: null, maxHops: 10, Direction.Outbound, default);

        result.Hops.Should().HaveCount(3);
        result.Visited.Should().BeEquivalentTo([A, B, C, D]);
    }

    [Fact]
    public async Task BFS_terminates_on_cyclic_graph_via_visited_set()
    {
        // A -> B -> C -> A : a 3-node cycle. Without visited-set tracking the
        // traversal would loop forever; with it, every node is enqueued exactly
        // once and the BFS halts.
        var store = new InMemoryRelationshipStore(
            Edge(A, B, RelationshipType.PublishesTo),
            Edge(B, C, RelationshipType.PublishesTo),
            Edge(C, A, RelationshipType.PublishesTo));

        var graph = new RelationshipGraph(store);
        var result = await graph.TraverseAsync(A, allowedTypes: null, maxHops: 100, Direction.Outbound, default);

        result.Visited.Should().BeEquivalentTo([A, B, C]);
        // Spanning-tree semantic: hops only record newly-discovered nodes.
        // The C→A back-edge is the cycle closer and is intentionally NOT
        // recorded (A was already visited at depth 0) — that's what bounds
        // the output and makes cycle protection visible to callers.
        result.Hops.Should().HaveCount(2);
    }

    [Fact]
    public async Task Direction_outbound_only_follows_source_to_target()
    {
        var store = new InMemoryRelationshipStore(
            Edge(A, B, RelationshipType.PublishesTo),
            Edge(C, A, RelationshipType.PublishesTo)); // inbound edge — should NOT be traversed outbound

        var graph = new RelationshipGraph(store);
        var result = await graph.TraverseAsync(A, allowedTypes: null, maxHops: 5, Direction.Outbound, default);

        result.Visited.Should().BeEquivalentTo([A, B]);
        result.Hops.Should().ContainSingle()
            .Which.To.Should().Be(B);
    }

    [Fact]
    public async Task Direction_inbound_only_follows_target_to_source()
    {
        var store = new InMemoryRelationshipStore(
            Edge(A, B, RelationshipType.PublishesTo), // outbound — should NOT be traversed inbound
            Edge(C, A, RelationshipType.PublishesTo)); // inbound — should be traversed

        var graph = new RelationshipGraph(store);
        var result = await graph.TraverseAsync(A, allowedTypes: null, maxHops: 5, Direction.Inbound, default);

        result.Visited.Should().BeEquivalentTo([A, C]);
        result.Hops.Should().ContainSingle()
            .Which.To.Should().Be(C);
    }

    [Fact]
    public async Task Direction_both_follows_either_endpoint()
    {
        var store = new InMemoryRelationshipStore(
            Edge(A, B, RelationshipType.PublishesTo),
            Edge(C, A, RelationshipType.PublishesTo));

        var graph = new RelationshipGraph(store);
        var result = await graph.TraverseAsync(A, allowedTypes: null, maxHops: 5, Direction.Both, default);

        result.Visited.Should().BeEquivalentTo([A, B, C]);
        result.Hops.Should().HaveCount(2);
    }

    [Fact]
    public async Task MaxHops_caps_traversal_depth()
    {
        var store = new InMemoryRelationshipStore(
            Edge(A, B, RelationshipType.PublishesTo),
            Edge(B, C, RelationshipType.ConsumedBy),
            Edge(C, D, RelationshipType.PartOfFlow));

        var graph = new RelationshipGraph(store);
        var result = await graph.TraverseAsync(A, allowedTypes: null, maxHops: 2, Direction.Outbound, default);

        // Only the first two hops should fire: A→B (depth 1), B→C (depth 2).
        // The C→D edge is not traversed because C is dequeued at depth 2 which
        // equals maxHops.
        result.Hops.Select(h => h.To).Should().BeEquivalentTo([B, C]);
        result.Visited.Should().BeEquivalentTo([A, B, C]);
    }

    [Fact]
    public async Task TypeFilter_restricts_traversal_to_allowed_relationship_types()
    {
        var store = new InMemoryRelationshipStore(
            Edge(A, B, RelationshipType.PublishesTo),  // included
            Edge(A, C, RelationshipType.Owns),          // excluded
            Edge(B, D, RelationshipType.PublishesTo)); // included

        var graph = new RelationshipGraph(store);
        var result = await graph.TraverseAsync(
            A,
            allowedTypes: [RelationshipType.PublishesTo],
            maxHops: 5,
            Direction.Outbound,
            default);

        result.Visited.Should().BeEquivalentTo([A, B, D]);
        result.Hops.All(h => h.Type == RelationshipType.PublishesTo).Should().BeTrue();
    }

    [Fact]
    public async Task MaxHops_zero_returns_only_the_start_node()
    {
        var store = new InMemoryRelationshipStore(
            Edge(A, B, RelationshipType.PublishesTo));

        var graph = new RelationshipGraph(store);
        var result = await graph.TraverseAsync(A, allowedTypes: null, maxHops: 0, Direction.Outbound, default);

        result.Hops.Should().BeEmpty();
        result.Visited.Should().BeEquivalentTo([A]);
    }

    private static Relationship Edge(ResourceId source, ResourceId target, RelationshipType type) => new()
    {
        Id = ResourceId.New(),
        SourceId = source,
        TargetId = target,
        Type = type,
        Audit = new AuditRecord(
            CreatedBy: new SystemPrincipalReference("test"),
            CreatedAt: DateTimeOffset.UnixEpoch,
            ModifiedBy: new SystemPrincipalReference("test"),
            ModifiedAt: DateTimeOffset.UnixEpoch),
    };

    // Test-only in-memory store. Implements only the surface RelationshipGraph
    // actually uses (QueryRelationshipsAsync); the remaining members throw
    // NotImplementedException so any accidental call surfaces loudly.
    private sealed class InMemoryRelationshipStore : ICanonicalResourceStore
    {
        private readonly IReadOnlyList<Relationship> _edges;

        public InMemoryRelationshipStore(params Relationship[] edges)
        {
            _edges = edges;
        }

        public async IAsyncEnumerable<Relationship> QueryRelationshipsAsync(
            RelationshipQuery query,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Pretend we await something so the compiler is satisfied.
            await Task.Yield();

            if (query is not RelationshipQuery.ByEndpoint byEndpoint)
            {
                throw new NotSupportedException("Only ByEndpoint queries are needed by RelationshipGraph.");
            }

            foreach (var edge in _edges)
            {
                if (edge.IsDeleted && !byEndpoint.IncludeDeleted)
                {
                    continue;
                }

                var match = byEndpoint.Direction switch
                {
                    Direction.Outbound => edge.SourceId == byEndpoint.EndpointId,
                    Direction.Inbound => edge.TargetId == byEndpoint.EndpointId,
                    Direction.Both => edge.SourceId == byEndpoint.EndpointId || edge.TargetId == byEndpoint.EndpointId,
                    _ => false,
                };

                if (match)
                {
                    yield return edge;
                }
            }
        }

        public Task<Resource?> GetAsync(ResourceId id, string resourceTypeDiscriminator, bool includeDeleted, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<Resource> QueryAsync(ResourceQuery query, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Resource> CreateAsync(Resource resource, PrincipalReference actor, string? sourceSystem, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Resource> UpdateAsync(Resource resource, PrincipalReference actor, string? sourceSystem, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Resource> SoftDeleteAsync(ResourceId id, string resourceTypeDiscriminator, ConcurrencyToken token, PrincipalReference actor, string? sourceSystem, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Resource> RestoreAsync(ResourceId id, string resourceTypeDiscriminator, ConcurrencyToken token, PrincipalReference actor, string? sourceSystem, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Relationship> CreateRelationshipAsync(Relationship relationship, PrincipalReference actor, string? sourceSystem, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Relationship?> GetRelationshipAsync(ResourceId id, bool includeDeleted, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
