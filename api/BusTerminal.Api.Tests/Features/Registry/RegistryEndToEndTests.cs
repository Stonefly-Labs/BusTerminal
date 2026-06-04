using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T067 [US1] [TEST]. End-to-end CRUD across all five entity types
// (Namespace → Queue → Topic → Subscription → Rule). Uses the in-memory
// fake stores so the test is hermetic; the Cosmos integration is exercised by
// CosmosRegistryEntityStoreTests (T034).
public sealed class RegistryEndToEndTests : IClassFixture<RegistryContractFactory>
{
    private readonly RegistryContractFactory _factory;

    public RegistryEndToEndTests(RegistryContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
    }

    [Fact]
    public async Task FullHierarchy_Roundtrip_WorksAcrossEveryEntityType()
    {
        using var client = _factory.CreateClient();

        var nsId = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/registry", new
        {
            id = nsId,
            entityType = "Namespace",
            name = "orders-prod",
            environment = "dev",
            status = "Active",
            source = "Manual",
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var topicId = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/registry", new
        {
            id = topicId,
            entityType = "Topic",
            name = "orders-events",
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = nsId,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var queueId = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/registry", new
        {
            id = queueId,
            entityType = "Queue",
            name = "orders-incoming",
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = nsId,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var subId = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/registry", new
        {
            id = subId,
            entityType = "Subscription",
            name = "payment-sub",
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = topicId,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var ruleId = Guid.NewGuid();
        (await client.PostAsJsonAsync("/api/registry", new
        {
            id = ruleId,
            entityType = "Rule",
            name = "payment-rule",
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = subId,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        // All 5 entities should be readable.
        foreach (var id in new[] { nsId, topicId, queueId, subId, ruleId })
        {
            (await client.GetAsync($"/api/registry/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Audit captured one Created event per entity.
        var audits = _factory.AuditStore.All();
        audits.Should().HaveCount(5);
    }
}
