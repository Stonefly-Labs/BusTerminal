using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Tests.Features.Registry._Shared;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Infrastructure.Persistence;

// Spec 006 / T034. CosmosRegistryEntityStore end-to-end coverage against the
// dev Cosmos account: CRUD happy path, ETag concurrency (stale write → 412),
// tombstone-then-delete behavior, child-count query, name-uniqueness query.
//
// The fixture skips automatically when the dev-cluster env vars are absent
// (see RegistryFixture.SkipIfUnconfigured).
[Collection("RegistryFixture")]
public class CosmosRegistryEntityStoreTests
{
    private readonly RegistryFixture _fixture;

    public CosmosRegistryEntityStoreTests(RegistryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_Read_Update_Delete_happy_path()
    {
        _fixture.SkipIfUnconfigured();

        var ns = MakeNamespace();
        var created = await _fixture.Store.CreateAsync(ns, default);
        try
        {
            created.Etag.Should().NotBeNullOrEmpty();

            var read = await _fixture.Store.GetAsync(created.Id, created.Environment, default);
            read.Should().NotBeNull();
            read!.Name.Should().Be(created.Name);

            var updated = await _fixture.Store.UpdateAsync(
                read with { Description = "updated" },
                read.Etag!,
                default);
            updated.Description.Should().Be("updated");
        }
        finally
        {
            await SafeDeleteAsync(created.Id, created.Environment, created.Etag);
        }
    }

    [Fact]
    public async Task Stale_etag_update_throws_concurrency_conflict()
    {
        _fixture.SkipIfUnconfigured();

        var ns = MakeNamespace();
        var created = await _fixture.Store.CreateAsync(ns, default);
        try
        {
            // First successful update advances the etag.
            var first = await _fixture.Store.UpdateAsync(
                created with { Description = "v2" },
                created.Etag!,
                default);

            // Second update with the original (stale) etag must throw.
            Func<Task> second = () => _fixture.Store.UpdateAsync(
                first with { Description = "v3" },
                created.Etag!,
                default);

            await second.Should().ThrowAsync<RegistryConcurrencyConflictException>();
        }
        finally
        {
            await SafeDeleteAsync(created.Id, created.Environment, etag: null);
        }
    }

    [Fact]
    public async Task Tombstone_is_excluded_from_get_and_list()
    {
        _fixture.SkipIfUnconfigured();

        var ns = MakeNamespace();
        var created = await _fixture.Store.CreateAsync(ns, default);

        await _fixture.Store.DeleteAsync(created.Id, created.Environment, created.Etag!, default);

        var read = await _fixture.Store.GetAsync(created.Id, created.Environment, default);
        read.Should().BeNull();

        var page = await _fixture.Store.ListAsync(
            new RegistryEntityListQuery(created.Environment),
            default);
        page.Items.Should().NotContain(e => e.Id == created.Id);
    }

    [Fact]
    public async Task CountChildren_returns_per_type_breakdown()
    {
        _fixture.SkipIfUnconfigured();

        var ns = MakeNamespace();
        var nsCreated = await _fixture.Store.CreateAsync(ns, default);
        var queue = await _fixture.Store.CreateAsync(MakeQueue(nsCreated.Id), default);
        var topic = await _fixture.Store.CreateAsync(MakeTopic(nsCreated.Id), default);
        try
        {
            var count = await _fixture.Store.CountChildrenAsync(
                nsCreated.Id, nsCreated.Environment, default);

            count.TotalChildren.Should().Be(2);
            count.ChildrenByType.Should().ContainKey(RegistryEntityType.Queue).WhoseValue.Should().Be(1);
            count.ChildrenByType.Should().ContainKey(RegistryEntityType.Topic).WhoseValue.Should().Be(1);
        }
        finally
        {
            await SafeDeleteAsync(queue.Id, queue.Environment, queue.Etag);
            await SafeDeleteAsync(topic.Id, topic.Environment, topic.Etag);
            await SafeDeleteAsync(nsCreated.Id, nsCreated.Environment, nsCreated.Etag);
        }
    }

    [Fact]
    public async Task FindByParentAndName_returns_match_when_exists()
    {
        _fixture.SkipIfUnconfigured();

        var ns = MakeNamespace();
        var nsCreated = await _fixture.Store.CreateAsync(ns, default);
        var queue = MakeQueue(nsCreated.Id);
        var queueCreated = await _fixture.Store.CreateAsync(queue, default);
        try
        {
            var found = await _fixture.Store.FindByParentAndNameAsync(
                queueCreated.ParentId,
                RegistryEntityType.Queue,
                queueCreated.Name,
                queueCreated.Environment,
                default);

            found.Should().NotBeNull();
            found!.Id.Should().Be(queueCreated.Id);
        }
        finally
        {
            await SafeDeleteAsync(queueCreated.Id, queueCreated.Environment, queueCreated.Etag);
            await SafeDeleteAsync(nsCreated.Id, nsCreated.Environment, nsCreated.Etag);
        }
    }

    private RegistryNamespace MakeNamespace() => new(
        id: Guid.NewGuid(),
        name: $"t{_fixture.TestRunId.ToString("N").Substring(0, 6)}ns",
        environment: _fixture.Environment,
        status: RegistryEntityStatus.Active,
        createdAtUtc: DateTimeOffset.UtcNow,
        updatedAtUtc: DateTimeOffset.UtcNow,
        source: RegistrySource.Manual);

    private RegistryQueue MakeQueue(Guid parentId) => new(
        id: Guid.NewGuid(),
        name: $"q{_fixture.TestRunId.ToString("N").Substring(0, 6)}",
        environment: _fixture.Environment,
        status: RegistryEntityStatus.Active,
        createdAtUtc: DateTimeOffset.UtcNow,
        updatedAtUtc: DateTimeOffset.UtcNow,
        source: RegistrySource.Manual,
        parentId: parentId);

    private RegistryTopic MakeTopic(Guid parentId) => new(
        id: Guid.NewGuid(),
        name: $"t{_fixture.TestRunId.ToString("N").Substring(0, 6)}",
        environment: _fixture.Environment,
        status: RegistryEntityStatus.Active,
        createdAtUtc: DateTimeOffset.UtcNow,
        updatedAtUtc: DateTimeOffset.UtcNow,
        source: RegistrySource.Manual,
        parentId: parentId);

    private async Task SafeDeleteAsync(Guid id, string environment, string? etag)
    {
        try
        {
            // Re-read to get the freshest etag so cleanup works even if a
            // test path updated the doc and didn't track the new value.
            var current = await _fixture.Store.GetAsync(id, environment, default);
            if (current is null) return;
            await _fixture.Store.DeleteAsync(id, environment, current.Etag!, default);
        }
        catch
        {
            // Best-effort cleanup — never fail teardown.
        }
    }
}
