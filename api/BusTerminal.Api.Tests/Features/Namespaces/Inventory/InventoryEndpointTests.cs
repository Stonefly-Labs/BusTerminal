using System.Net;
using System.Net.Http;
using System.Text.Json;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Namespaces.Inventory;

// Spec 008 / T095 + T102 / US2. Contract tests for GET /api/namespaces
// (inventory). Covers pagination, environment + lifecycle + validation
// filters, tag filter (key + value), partial-name search across displayName /
// businessUnit / name, sort by every supported column, includeArchived
// toggle, and the defaults (page size 25, sort = lastValidatedAt_desc).
public sealed class InventoryEndpointTests : IClassFixture<NamespacesContractFactory>
{
    private readonly NamespacesContractFactory _factory;

    public InventoryEndpointTests(NamespacesContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
    }

    [Fact]
    public async Task Get_NoOnboarded_ReturnsEmpty()
    {
        using var client = AuthenticatedClient();

        var response = await client.GetAsync("/api/namespaces");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await ReadJson(response);
        doc.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Get_ReturnsOnlyOnboardedSource()
    {
        SeedNamespace("Manual NS", environment: "dev", source: RegistrySource.Manual);
        SeedNamespace("Onboarded NS", environment: "dev", source: RegistrySource.Onboarded);

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces");

        var doc = await ReadJson(response);
        var items = doc.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("displayName").GetString().Should().Be("Onboarded NS");
        items[0].GetProperty("source").GetString().Should().Be("Onboarded");
    }

    [Fact]
    public async Task Get_FilterByEnvironment_NarrowsResults()
    {
        SeedNamespace("Orders DEV", environment: "dev");
        SeedNamespace("Orders PROD", environment: "prod");

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces?environment=prod");

        var items = (await ReadJson(response)).GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("displayName").GetString().Should().Be("Orders PROD");
    }

    [Fact]
    public async Task Get_FilterByLifecycleStatus_MultiValue_OrsResults()
    {
        SeedNamespace("NS Active", lifecycle: LifecycleStatus.Active);
        SeedNamespace("NS Disabled", lifecycle: LifecycleStatus.Disabled);
        SeedNamespace("NS Archived", lifecycle: LifecycleStatus.Archived);

        using var client = AuthenticatedClient();
        // Pass two filter values + includeArchived so the Archived row remains eligible.
        var response = await client.GetAsync(
            "/api/namespaces?lifecycleStatus=Disabled&lifecycleStatus=Archived&includeArchived=true");

        var items = (await ReadJson(response)).GetProperty("items");
        items.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Get_FilterByValidationStatus_NarrowsResults()
    {
        SeedNamespace("NS Healthy", validation: ValidationStatus.Healthy);
        SeedNamespace("NS Degraded", validation: ValidationStatus.Degraded);
        SeedNamespace("NS Unhealthy", validation: ValidationStatus.Unhealthy);

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces?validationStatus=Degraded");

        var items = (await ReadJson(response)).GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("validationStatus").GetString().Should().Be("Degraded");
    }

    [Fact]
    public async Task Get_FilterByTagKeyOnly_MatchesAny()
    {
        SeedNamespace("Payments", tags: new[] { new RegistryTag("team", "payments") });
        SeedNamespace("Logistics", tags: new[] { new RegistryTag("team", "logistics") });
        SeedNamespace("NoTags");

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces?tagKey=team");

        var items = (await ReadJson(response)).GetProperty("items");
        items.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Get_FilterByTagKeyAndValue_MatchesExact()
    {
        SeedNamespace("Payments", tags: new[] { new RegistryTag("team", "payments") });
        SeedNamespace("Logistics", tags: new[] { new RegistryTag("team", "logistics") });

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces?tagKey=team&tagValue=payments");

        var items = (await ReadJson(response)).GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("displayName").GetString().Should().Be("Payments");
    }

    [Fact]
    public async Task Get_SearchByPartialDisplayName_Matches()
    {
        SeedNamespace("Orders Prod EUS2");
        SeedNamespace("Inventory Dev");

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces?q=orders");

        var items = (await ReadJson(response)).GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("displayName").GetString().Should().Be("Orders Prod EUS2");
    }

    [Fact]
    public async Task Get_SearchByPartialBusinessUnit_Matches()
    {
        SeedNamespace("Alpha", businessUnit: "Payments");
        SeedNamespace("Beta", businessUnit: "Logistics");

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces?q=pay");

        var items = (await ReadJson(response)).GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("displayName").GetString().Should().Be("Alpha");
    }

    [Fact]
    public async Task Get_DefaultArchivedHidden_ExcludesArchived()
    {
        SeedNamespace("Active NS", lifecycle: LifecycleStatus.Active);
        SeedNamespace("Archived NS", lifecycle: LifecycleStatus.Archived);

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces");

        var items = (await ReadJson(response)).GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("displayName").GetString().Should().Be("Active NS");
    }

    [Fact]
    public async Task Get_IncludeArchivedTrue_SurfacesArchived()
    {
        SeedNamespace("Active NS", lifecycle: LifecycleStatus.Active);
        SeedNamespace("Archived NS", lifecycle: LifecycleStatus.Archived);

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces?includeArchived=true");

        var items = (await ReadJson(response)).GetProperty("items");
        items.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Get_SortByDisplayNameAsc_OrdersAlphabetically()
    {
        SeedNamespace("Zeta");
        SeedNamespace("Alpha");
        SeedNamespace("Mu");

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces?sort=displayName_asc");

        var items = (await ReadJson(response)).GetProperty("items");
        items[0].GetProperty("displayName").GetString().Should().Be("Alpha");
        items[1].GetProperty("displayName").GetString().Should().Be("Mu");
        items[2].GetProperty("displayName").GetString().Should().Be("Zeta");
    }

    [Fact]
    public async Task Get_DefaultSort_IsLastValidatedAtDesc()
    {
        var older = DateTimeOffset.UtcNow.AddDays(-3);
        var newer = DateTimeOffset.UtcNow.AddHours(-1);
        SeedNamespace("Older", lastValidatedAt: older);
        SeedNamespace("Newer", lastValidatedAt: newer);

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces");

        var items = (await ReadJson(response)).GetProperty("items");
        items[0].GetProperty("displayName").GetString().Should().Be("Newer");
        items[1].GetProperty("displayName").GetString().Should().Be("Older");
    }

    [Fact]
    public async Task Get_PageSize_LimitsResultCount()
    {
        for (var i = 0; i < 30; i++)
        {
            SeedNamespace($"NS-{i:D2}");
        }

        using var client = AuthenticatedClient();
        var response = await client.GetAsync("/api/namespaces?pageSize=5");

        var items = (await ReadJson(response)).GetProperty("items");
        items.GetArrayLength().Should().Be(5);
    }

    // === helpers ===

    private HttpClient AuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(MockAuthenticationHandler.MockRolesHeader, "BusTerminal.Reader");
        return client;
    }

    private void SeedNamespace(
        string displayName,
        string environment = "dev",
        RegistrySource source = RegistrySource.Onboarded,
        LifecycleStatus? lifecycle = null,
        ValidationStatus? validation = null,
        DateTimeOffset? lastValidatedAt = null,
        IReadOnlyList<RegistryTag>? tags = null,
        string? businessUnit = null)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var entity = new RegistryNamespace(
            id: id,
            name: $"ns-{id:N}".Substring(0, 20),
            environment: environment,
            status: RegistryEntityStatus.Active,
            createdAtUtc: now,
            updatedAtUtc: now,
            source: source,
            fullyQualifiedName: $"ns-{id:N}".Substring(0, 20),
            description: null,
            tags: tags,
            owner: null,
            azureResourceId: source == RegistrySource.Onboarded
                ? $"/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg-{id:N}".Substring(0, 100)
                : null,
            metadata: null,
            etag: null)
        {
            DisplayName = displayName,
            BusinessUnit = businessUnit,
            LifecycleStatus = lifecycle ?? (source == RegistrySource.Onboarded ? LifecycleStatus.Active : null),
            ValidationStatus = validation ?? (source == RegistrySource.Onboarded ? ValidationStatus.Healthy : null),
            LastValidationRunId = source == RegistrySource.Onboarded ? Guid.NewGuid() : null,
            LastValidatedAtUtc = lastValidatedAt ?? (source == RegistrySource.Onboarded ? now : null),
            Ownership = source == RegistrySource.Onboarded ? new OwnershipBlock(
                PrimaryOwner: new OwnershipAssignment(
                    Role: OwnershipRole.PrimaryOwner,
                    PrincipalType: PrincipalType.User,
                    ObjectId: Guid.NewGuid(),
                    DisplayNameSnapshot: "Test Owner",
                    AssignedAtUtc: now,
                    AssignedBy: Guid.NewGuid()),
                SecondaryOwners: Array.Empty<OwnershipAssignment>(),
                TechnicalStewards: Array.Empty<OwnershipAssignment>(),
                SupportContacts: Array.Empty<OwnershipAssignment>()) : null,
            OnboardingActor = source == RegistrySource.Onboarded ? new OnboardingActor(
                ObjectId: Guid.NewGuid(),
                DisplayNameSnapshot: "actor",
                OnboardedAtUtc: now) : null,
        };

        _factory.EntityStore.CreateAsync(entity, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content).RootElement.Clone();
    }
}
