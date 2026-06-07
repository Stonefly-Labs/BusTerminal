using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Tests.Features.Registry._Shared;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Infrastructure.Persistence;

// Spec 006 / T035. Append + entity-scoped read coverage. No edit/delete
// surface because the IAuditEventStore contract only exposes Write +
// ListForEntity (FR-034 append-only).
[Collection("RegistryFixture")]
[Trait("Category", "Integration")]
public class CosmosAuditEventStoreTests
{
    private readonly RegistryFixture _fixture;

    public CosmosAuditEventStoreTests(RegistryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Append_then_list_returns_event()
    {
        if (!_fixture.ShouldRun()) return;

        var entityId = Guid.NewGuid();
        var evt = NewEvent(entityId, AuditEventType.Created, DateTimeOffset.UtcNow);
        await _fixture.AuditStore.WriteAsync(evt, default);

        var events = await _fixture.AuditStore.ListForEntityAsync(entityId, limit: 10, default);

        events.Should().ContainSingle(e => e.Id == evt.Id);
    }

    [Fact]
    public async Task List_orders_newest_first_and_honors_limit()
    {
        if (!_fixture.ShouldRun()) return;

        var entityId = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow.AddSeconds(-10);
        var older = NewEvent(entityId, AuditEventType.Created, t0);
        var newer = NewEvent(entityId, AuditEventType.Updated, t0.AddSeconds(5));
        var newest = NewEvent(entityId, AuditEventType.StatusChanged, t0.AddSeconds(9));

        await _fixture.AuditStore.WriteAsync(older, default);
        await _fixture.AuditStore.WriteAsync(newer, default);
        await _fixture.AuditStore.WriteAsync(newest, default);

        var events = await _fixture.AuditStore.ListForEntityAsync(entityId, limit: 2, default);

        events.Should().HaveCount(2);
        events[0].Id.Should().Be(newest.Id);
        events[1].Id.Should().Be(newer.Id);
    }

    [Fact]
    public async Task Append_after_a_read_does_not_modify_existing_events()
    {
        if (!_fixture.ShouldRun()) return;

        var entityId = Guid.NewGuid();
        var first = NewEvent(entityId, AuditEventType.Created, DateTimeOffset.UtcNow);
        await _fixture.AuditStore.WriteAsync(first, default);

        // Append-only by contract — there is no "update" surface to attempt.
        var second = NewEvent(entityId, AuditEventType.Updated, first.Timestamp.AddSeconds(1));
        await _fixture.AuditStore.WriteAsync(second, default);

        var events = await _fixture.AuditStore.ListForEntityAsync(entityId, limit: 10, default);
        events.Should().HaveCountGreaterThanOrEqualTo(2);
        events.Should().Contain(e => e.Id == first.Id);
        events.Should().Contain(e => e.Id == second.Id);
    }

    private AuditEvent NewEvent(Guid entityId, AuditEventType eventType, DateTimeOffset timestamp) => new(
        Id: Guid.NewGuid(),
        EntityId: entityId,
        EntityType: RegistryEntityType.Queue,
        Environment: _fixture.Environment,
        EventType: eventType,
        Timestamp: timestamp,
        Actor: new AuditActor("test-principal-oid", "Tester"),
        ChangeSummary: $"{eventType} for test run {_fixture.TestRunId:N}",
        WasForceOverwrite: false,
        CorrelationId: Guid.NewGuid().ToString("N"));
}
