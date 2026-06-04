using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T062 [US1] [TEST]. Contract test for POST /api/registry.
// Asserts response shape conforms to RegistryEntity schema, ETag header
// present, Location header present, audit event emitted.
public sealed class CreateEntityEndpointTests : IClassFixture<RegistryContractFactory>
{
    private readonly RegistryContractFactory _factory;

    public CreateEntityEndpointTests(RegistryContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
    }

    [Fact]
    public async Task PostNamespace_HappyPath_Returns201_WithEtagAndLocation()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        var request = new
        {
            id,
            entityType = "Namespace",
            name = "orders-prod",
            environment = "dev",
            status = "Active",
            source = "Manual",
            owner = "payments-platform",
        };

        var response = await client.PostAsJsonAsync("/api/registry", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.ETag.Should().NotBeNull();
        response.Headers.Location?.ToString().Should().Be($"/api/registry/{id:D}");

        var doc = await ReadJson(response);
        doc.GetProperty("id").GetGuid().Should().Be(id);
        doc.GetProperty("entityType").GetString().Should().Be("Namespace");
        doc.GetProperty("status").GetString().Should().Be("Active");
        doc.GetProperty("source").GetString().Should().Be("Manual");

        var audits = _factory.AuditStore.All();
        audits.Should().ContainSingle(a => a.EntityId == id && a.EventType == AuditEventType.Created);
    }

    [Theory]
    [InlineData("Queue", "orders-incoming")]
    [InlineData("Topic", "orders-events")]
    public async Task PostChild_RequiresAndValidatesParent(string childType, string childName)
    {
        using var client = _factory.CreateClient();
        // Seed a namespace parent first.
        var nsId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/registry", new
        {
            id = nsId,
            entityType = "Namespace",
            name = "orders-prod",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });

        // Child create with a bogus parent → 400.
        var bad = await client.PostAsJsonAsync("/api/registry", new
        {
            id = Guid.NewGuid(),
            entityType = childType,
            name = childName,
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = Guid.NewGuid(),
        });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Child create with the correct parent → 201.
        var good = await client.PostAsJsonAsync("/api/registry", new
        {
            id = Guid.NewGuid(),
            entityType = childType,
            name = childName,
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = nsId,
        });
        good.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostQueue_DuplicateName_Returns409()
    {
        using var client = _factory.CreateClient();
        var nsId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/registry", new
        {
            id = nsId,
            entityType = "Namespace",
            name = "orders-prod",
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        await client.PostAsJsonAsync("/api/registry", new
        {
            id = Guid.NewGuid(),
            entityType = "Queue",
            name = "duplicates",
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = nsId,
        });

        var clash = await client.PostAsJsonAsync("/api/registry", new
        {
            id = Guid.NewGuid(),
            entityType = "Queue",
            name = "duplicates",
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = nsId,
        });

        clash.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostNamespace_MissingRequiredField_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/registry", new
        {
            id = Guid.NewGuid(),
            entityType = "Namespace",
            // missing name
            environment = "dev",
            status = "Active",
            source = "Manual",
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostQueue_UnderDeprecatedParent_AuditPrefixed()
    {
        using var client = _factory.CreateClient();
        var nsId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/registry", new
        {
            id = nsId,
            entityType = "Namespace",
            name = "orders-prod",
            environment = "dev",
            status = "Deprecated",
            source = "Manual",
        });

        await client.PostAsJsonAsync("/api/registry", new
        {
            id = Guid.NewGuid(),
            entityType = "Queue",
            name = "under-deprecated",
            environment = "dev",
            status = "Active",
            source = "Manual",
            parentId = nsId,
        });

        _factory.AuditStore.All()
            .Should().Contain(a => a.EventType == AuditEventType.Created
                                   && a.ChangeSummary.StartsWith(RegistryAuditFactory.DeprecatedParentPrefix));
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }
}
