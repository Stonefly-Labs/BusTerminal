using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.StartDiscovery;

// Spec 009 / T031 + T033 + T034. Contract + coalescing + auth tests for
// POST /api/namespaces/{id}/discover. All three concerns live in this file
// because they share the same seeded-namespace fixture.
public sealed class StartDiscoveryContractTests : IClassFixture<DiscoveryContractFactory>
{
    private readonly DiscoveryContractFactory _factory;

    public StartDiscoveryContractTests(DiscoveryContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.LockStore.GetType();
        _factory.RunStore.GetType();
        _factory.Publisher.Published.Clear();
    }

    // ── T031 — contract shape ────────────────────────────────────────────

    [Fact]
    public async Task StartDiscovery_HappyPath_Returns202_WithExpectedShape()
    {
        var nsId = await SeedNamespaceAsync();
        using var client = AdminClient();

        var response = await client.PostAsync($"/api/namespaces/{nsId:D}/discover", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await ReadJson(response);
        body.GetProperty("discoveryRunId").GetString().Should().StartWith("dr_");
        body.GetProperty("namespaceId").GetString().Should().Be(nsId.ToString("D"));
        body.GetProperty("status").GetString().Should().Be("Queued");
        body.GetProperty("coalescedFromExisting").GetBoolean().Should().BeFalse();
        body.GetProperty("startedUtc").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StartDiscovery_NewRun_PublishesEnvelope()
    {
        var nsId = await SeedNamespaceAsync();
        using var client = AdminClient();

        await client.PostAsync($"/api/namespaces/{nsId:D}/discover", content: null);

        _factory.Publisher.Published.Should().HaveCount(1);
        _factory.Publisher.Published[0].NamespaceId.Should().Be(nsId.ToString("D"));
    }

    [Fact]
    public async Task StartDiscovery_UnknownNamespace_Returns404()
    {
        using var client = AdminClient();
        var response = await client.PostAsync($"/api/namespaces/{Guid.NewGuid():D}/discover", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartDiscovery_DisabledNamespace_Returns409()
    {
        var nsId = await SeedNamespaceAsync(LifecycleStatus.Disabled);
        using var client = AdminClient();

        var response = await client.PostAsync($"/api/namespaces/{nsId:D}/discover", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── T033 — FR-003 coalescing ─────────────────────────────────────────

    [Fact]
    public async Task StartDiscovery_BackToBack_CoalescesOnInFlightRun()
    {
        var nsId = await SeedNamespaceAsync();
        using var client = AdminClient();

        var first = await client.PostAsync($"/api/namespaces/{nsId:D}/discover", content: null);
        var second = await client.PostAsync($"/api/namespaces/{nsId:D}/discover", content: null);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var firstBody = await ReadJson(first);
        var secondBody = await ReadJson(second);
        secondBody.GetProperty("coalescedFromExisting").GetBoolean().Should().BeTrue();
        secondBody.GetProperty("discoveryRunId").GetString().Should().Be(
            firstBody.GetProperty("discoveryRunId").GetString());

        // Publisher only gets the first envelope — the coalesced second request
        // does NOT re-enqueue (the worker is already processing).
        _factory.Publisher.Published.Should().HaveCount(1);
    }

    // ── T034 — FR-027 authorization ──────────────────────────────────────

    [Fact]
    public async Task StartDiscovery_WithoutAdministratorRole_Returns403()
    {
        var nsId = await SeedNamespaceAsync();
        using var client = ReaderClient();

        var response = await client.PostAsync($"/api/namespaces/{nsId:D}/discover", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StartDiscovery_WithoutAnyRoleHeader_Returns403()
    {
        // Note: in Development the MockAuthenticationHandler always
        // *authenticates* the dev principal (no anon path), so a missing
        // X-Mock-Roles header surfaces as 403 (no NamespaceAdmin role)
        // rather than 401. The 401 path is exercised by the JwtBearer
        // pipeline in non-dev environments.
        var nsId = await SeedNamespaceAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/namespaces/{nsId:D}/discover", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private async Task<Guid> SeedNamespaceAsync(LifecycleStatus lifecycle = LifecycleStatus.Active)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var entity = new RegistryNamespace(
            id: id,
            name: "orders-prod",
            environment: "dev",
            status: RegistryEntityStatus.Active,
            createdAtUtc: now,
            updatedAtUtc: now,
            source: RegistrySource.Onboarded,
            fullyQualifiedName: "orders-prod",
            description: "Seeded for spec 009 tests",
            tags: null,
            owner: null,
            azureResourceId: "/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg-payments-prod/providers/Microsoft.ServiceBus/namespaces/orders-prod",
            metadata: null,
            etag: null)
        {
            DisplayName = "Orders Prod",
            SubscriptionId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ResourceGroup = "rg-payments-prod",
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Region = "eastus2",
            LifecycleStatus = lifecycle,
        };
        await _factory.EntityStore.CreateAsync(entity, CancellationToken.None);
        return id;
    }

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(MockAuthenticationHandler.MockRolesHeader, "BusTerminal.NamespaceAdministrator");
        return client;
    }

    private HttpClient ReaderClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(MockAuthenticationHandler.MockRolesHeader, "BusTerminal.Reader");
        return client;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content).RootElement.Clone();
    }
}
