using BusTerminal.Api.Features.Registry.Shared;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry._Shared;

// Spec 006 / T043. Three cases: zero children, single child, multi-type
// children breakdown.
public class ChildCountCheckerTests
{
    [Fact]
    public async Task Returns_null_when_no_children()
    {
        var store = new FakeStore(new ChildCount(0, new Dictionary<RegistryEntityType, int>()));
        var checker = new ChildCountChecker(store);

        var result = await checker.CheckAsync(Guid.NewGuid(), "dev", instance: null, default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Returns_response_with_count_when_single_child()
    {
        var store = new FakeStore(new ChildCount(
            TotalChildren: 1,
            ChildrenByType: new Dictionary<RegistryEntityType, int> { [RegistryEntityType.Queue] = 1 }));
        var checker = new ChildCountChecker(store);
        var entityId = Guid.NewGuid();

        var result = await checker.CheckAsync(entityId, "dev", instance: $"/api/registry/{entityId:D}", default);

        result.Should().NotBeNull();
        result!.TotalChildren.Should().Be(1);
        result.ChildrenByType.Should().ContainKey(RegistryEntityType.Queue);
        result.Code.Should().Be("HasChildren");
        result.Status.Should().Be(409);
        result.Instance.Should().Be($"/api/registry/{entityId:D}");
    }

    [Fact]
    public async Task Returns_breakdown_for_multi_type_children()
    {
        var store = new FakeStore(new ChildCount(
            TotalChildren: 5,
            ChildrenByType: new Dictionary<RegistryEntityType, int>
            {
                [RegistryEntityType.Queue] = 2,
                [RegistryEntityType.Topic] = 3,
            }));
        var checker = new ChildCountChecker(store);

        var result = await checker.CheckAsync(Guid.NewGuid(), "dev", instance: null, default);

        result.Should().NotBeNull();
        result!.TotalChildren.Should().Be(5);
        result.ChildrenByType[RegistryEntityType.Queue].Should().Be(2);
        result.ChildrenByType[RegistryEntityType.Topic].Should().Be(3);
    }

    private sealed class FakeStore : IRegistryEntityStore
    {
        private readonly ChildCount _result;
        public FakeStore(ChildCount result) { _result = result; }

        public Task<ChildCount> CountChildrenAsync(Guid parentId, string environment, CancellationToken cancellationToken)
            => Task.FromResult(_result);

        // Not exercised by these tests — throw if anything else calls in.
        public Task<RegistryEntity?> GetAsync(Guid id, string environment, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RegistryEntityPage> ListAsync(RegistryEntityListQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RegistryEntity> CreateAsync(RegistryEntity entity, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RegistryEntity> UpdateAsync(RegistryEntity entity, string ifMatchEtag, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task DeleteAsync(Guid id, string environment, string ifMatchEtag, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RegistryEntity?> FindByParentAndNameAsync(Guid? parentId, RegistryEntityType entityType, string name, string environment, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RegistryEntity?> FindParentAsync(Guid parentId, RegistryEntityType expectedParentType, string environment, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> ListDistinctEnvironmentsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
