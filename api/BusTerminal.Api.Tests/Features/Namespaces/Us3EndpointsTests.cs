using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Namespaces;

// Spec 008 / T117–T121 (consolidated) — endpoint contract tests for US3
// (metadata / ownership / lifecycle / validation-runs).
//
// Consolidated into a single test class for two reasons:
//   1. Every endpoint reuses the same seed helper (SeedOnboardedAsync) so
//      keeping them together avoids copy/paste drift.
//   2. The Phase 3/4 precedent (NamespaceEndpointsTests.cs,
//      DetailsEndpointTests.cs) already mixes per-endpoint suites and per-slice
//      flow tests in one file when they share a fixture — this is the same
//      shape applied to US3.
public sealed class Us3EndpointsTests : IClassFixture<NamespacesContractFactory>
{
    private const string ArmId = "/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg-payments-prod/providers/Microsoft.ServiceBus/namespaces/orders-prod";

    private readonly NamespacesContractFactory _factory;

    public Us3EndpointsTests(NamespacesContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
        _factory.ArmProbe.ExistenceOutcome = ValidationCheckOutcome.Pass;
        _factory.ArmProbe.AccessibilityOutcome = ValidationCheckOutcome.Pass;
        _factory.ArmProbe.RequiredPermissionsOutcome = ValidationCheckOutcome.Pass;
        _factory.ArmProbe.IdentityAuthorizationOutcome = ValidationCheckOutcome.Pass;
        _factory.ArmProbe.ApiReachabilityOutcome = ValidationCheckOutcome.Pass;
    }

    // ============ T117 — PUT /api/namespaces/{id}/metadata ============

    [Fact]
    public async Task UpdateMetadata_HappyPath_Returns200AndEmitsAudit()
    {
        var (id, etag, _) = await SeedOnboardedAsync();
        using var client = AdminClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, etag);

        var response = await client.PutAsJsonAsync($"/api/namespaces/{id:D}/metadata", new
        {
            id,
            displayName = "Orders Production (renamed)",
            description = "Refined description",
            businessUnit = "Payments",
            productOrApplication = "OrderingPlatform",
            costCenter = "CC-100",
            notes = "Updated by admin.",
            tags = new[] { new { key = "tier", value = "gold" } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();

        var body = await ReadJson(response);
        body.GetProperty("displayName").GetString().Should().Be("Orders Production (renamed)");
        body.GetProperty("businessUnit").GetString().Should().Be("Payments");
        body.GetProperty("tags").GetArrayLength().Should().Be(1);

        _factory.AuditStore.All().Should().ContainSingle(a =>
            a.EntityId == id && a.EventType == AuditEventType.NamespaceMetadataUpdated);
    }

    [Fact]
    public async Task UpdateMetadata_AzureIdentifierInBody_Returns400()
    {
        var (id, etag, _) = await SeedOnboardedAsync();
        using var client = AdminClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, etag);

        var response = await client.PutAsJsonAsync($"/api/namespaces/{id:D}/metadata", new
        {
            id,
            displayName = "Orders",
            azureResourceId = "/subscriptions/x/...",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMetadata_MissingIfMatch_Returns428()
    {
        var (id, _, _) = await SeedOnboardedAsync();
        using var client = AdminClient();

        var response = await client.PutAsJsonAsync($"/api/namespaces/{id:D}/metadata", new
        {
            id,
            displayName = "Orders",
        });

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task UpdateMetadata_StaleEtag_Returns409Conflict()
    {
        var (id, _, _) = await SeedOnboardedAsync();
        using var client = AdminClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, "\"stale-etag-value\"");

        var response = await client.PutAsJsonAsync($"/api/namespaces/{id:D}/metadata", new
        {
            id,
            displayName = "Orders Updated",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var doc = await ReadJson(response);
        doc.GetProperty("code").GetString().Should().Be("ConcurrencyConflict");
    }

    [Fact]
    public async Task UpdateMetadata_NonAdmin_Returns403()
    {
        var (id, etag, _) = await SeedOnboardedAsync();
        using var client = ReaderClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, etag);

        var response = await client.PutAsJsonAsync($"/api/namespaces/{id:D}/metadata", new
        {
            id,
            displayName = "Orders",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ============ T118 — PUT /api/namespaces/{id}/ownership ============

    [Fact]
    public async Task UpdateOwnership_HappyPath_Returns200AndEmitsAudit()
    {
        var (id, etag, _) = await SeedOnboardedAsync();
        using var client = AdminClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, etag);

        var response = await client.PutAsJsonAsync($"/api/namespaces/{id:D}/ownership", new
        {
            id,
            ownership = BuildOwnership(primaryName: "Carol", stewards: 1),
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();

        var body = await ReadJson(response);
        body.GetProperty("ownership").GetProperty("primaryOwner").GetProperty("displayNameSnapshot").GetString().Should().Be("Carol");
        body.GetProperty("ownership").GetProperty("technicalStewards").GetArrayLength().Should().Be(1);

        _factory.AuditStore.All().Should().ContainSingle(a =>
            a.EntityId == id && a.EventType == AuditEventType.NamespaceOwnershipUpdated);
    }

    [Fact]
    public async Task UpdateOwnership_MissingPrimaryOwner_Returns400()
    {
        var (id, etag, _) = await SeedOnboardedAsync();
        using var client = AdminClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, etag);

        var response = await client.PutAsJsonAsync($"/api/namespaces/{id:D}/ownership", new
        {
            id,
            ownership = new
            {
                primaryOwner = (object?)null,
                secondaryOwners = Array.Empty<object>(),
                technicalStewards = Array.Empty<object>(),
                supportContacts = Array.Empty<object>(),
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateOwnership_NonAdmin_Returns403()
    {
        var (id, etag, _) = await SeedOnboardedAsync();
        using var client = ReaderClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, etag);

        var response = await client.PutAsJsonAsync($"/api/namespaces/{id:D}/ownership", new
        {
            id,
            ownership = BuildOwnership(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ============ T119 — POST /api/namespaces/{id}/lifecycle ============

    [Fact]
    public async Task Lifecycle_Disable_FromActive_Returns200AndAudits()
    {
        var (id, etag, _) = await SeedOnboardedAsync();
        using var client = AdminClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, etag);

        var response = await client.PostAsJsonAsync($"/api/namespaces/{id:D}/lifecycle", new
        {
            id,
            action = "disable",
            reason = "Temporarily disabled for migration.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJson(response);
        body.GetProperty("lifecycleStatus").GetString().Should().Be("Disabled");

        var audit = _factory.AuditStore.All()
            .Single(a => a.EntityId == id && a.EventType == AuditEventType.NamespaceLifecycleTransitioned);
        audit.LifecycleReason.Should().Be("Temporarily disabled for migration.");
    }

    [Fact]
    public async Task Lifecycle_Enable_FromDisabled_AutoRevalidates()
    {
        var (id, _, entity) = await SeedOnboardedAsync(lifecycle: LifecycleStatus.Disabled);
        // refresh ETag because seeding fresh entity returned a new ETag
        var fresh = await _factory.EntityStore.FindByIdAsync(id, CancellationToken.None) ?? entity;

        using var client = AdminClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, fresh.Etag!);

        var response = await client.PostAsJsonAsync($"/api/namespaces/{id:D}/lifecycle", new
        {
            id,
            action = "enable",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJson(response);
        body.GetProperty("lifecycleStatus").GetString().Should().Be("Active");
        body.GetProperty("validationStatus").GetString().Should().Be("Healthy");

        // A fresh ValidationRun was appended on enable.
        _factory.RunStore.All().Should().Contain(r => r.NamespaceId == id && r.AggregateStatus == ValidationStatus.Healthy);
    }

    [Fact]
    public async Task Lifecycle_Archive_RequiresReason_Returns400WhenMissing()
    {
        var (id, etag, _) = await SeedOnboardedAsync();
        using var client = AdminClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, etag);

        var response = await client.PostAsJsonAsync($"/api/namespaces/{id:D}/lifecycle", new
        {
            id,
            action = "archive",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lifecycle_ImpermissibleTransition_Returns400()
    {
        var (id, etag, _) = await SeedOnboardedAsync(lifecycle: LifecycleStatus.Archived);
        var fresh = await _factory.EntityStore.FindByIdAsync(id, CancellationToken.None);

        using var client = AdminClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, fresh!.Etag!);

        // Archived → Disable is not in the permitted table (only Restore is).
        var response = await client.PostAsJsonAsync($"/api/namespaces/{id:D}/lifecycle", new
        {
            id,
            action = "disable",
            reason = "trying to bypass restore",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lifecycle_NonAdmin_Returns403()
    {
        var (id, etag, _) = await SeedOnboardedAsync();
        using var client = ReaderClient();
        client.DefaultRequestHeaders.Add(HeaderNames.IfMatch, etag);

        var response = await client.PostAsJsonAsync($"/api/namespaces/{id:D}/lifecycle", new
        {
            id,
            action = "disable",
            reason = "no role",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ============ T120 — POST /api/namespaces/{id}/validation-runs ============

    [Fact]
    public async Task RunValidation_HappyPath_Returns201_PersistsRun_AdvancesNamespace()
    {
        var (id, _, _) = await SeedOnboardedAsync();
        var runsBefore = _factory.RunStore.All().Count(r => r.NamespaceId == id);

        using var client = AdminClient();
        var response = await client.PostAsync($"/api/namespaces/{id:D}/validation-runs", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJson(response);
        body.GetProperty("aggregateStatus").GetString().Should().Be("Healthy");

        _factory.RunStore.All().Count(r => r.NamespaceId == id).Should().Be(runsBefore + 1);
        _factory.AuditStore.All().Should().Contain(a => a.EntityId == id && a.EventType == AuditEventType.NamespaceValidationExecuted);
    }

    [Fact]
    public async Task RunValidation_ArchivedNamespace_Returns409()
    {
        var (id, _, _) = await SeedOnboardedAsync(lifecycle: LifecycleStatus.Archived);
        using var client = AdminClient();

        var response = await client.PostAsync($"/api/namespaces/{id:D}/validation-runs", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RunValidation_NonAdmin_Returns403()
    {
        var (id, _, _) = await SeedOnboardedAsync();
        using var client = ReaderClient();

        var response = await client.PostAsync($"/api/namespaces/{id:D}/validation-runs", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ============ T121 — GET /api/namespaces/{id}/validation-runs(/{runId}) ============

    [Fact]
    public async Task ListValidationRuns_AuthN_ReturnsItems()
    {
        var (id, _, _) = await SeedOnboardedAsync();
        using var client = ReaderClient();

        var response = await client.GetAsync($"/api/namespaces/{id:D}/validation-runs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await ReadJson(response);
        doc.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetValidationRun_AuthN_ReturnsSingle()
    {
        var (id, _, entity) = await SeedOnboardedAsync();
        var runId = entity.LastValidationRunId!.Value;
        using var client = ReaderClient();

        var response = await client.GetAsync($"/api/namespaces/{id:D}/validation-runs/{runId:D}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await ReadJson(response);
        doc.GetProperty("id").GetGuid().Should().Be(runId);
    }

    [Fact]
    public async Task GetValidationRun_UnknownId_Returns404()
    {
        var (id, _, _) = await SeedOnboardedAsync();
        using var client = ReaderClient();

        var response = await client.GetAsync($"/api/namespaces/{id:D}/validation-runs/{Guid.NewGuid():D}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ============ helpers ============

    private async Task<(Guid Id, string Etag, RegistryNamespace Entity)> SeedOnboardedAsync(LifecycleStatus lifecycle = LifecycleStatus.Active)
    {
        var id = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var subId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var run = new ValidationRun(
            Id: runId,
            NamespaceId: id,
            ExecutedAtUtc: now,
            ExecutedBy: Guid.NewGuid(),
            ExecutedByDisplayNameSnapshot: "actor",
            AzureResourceIdAtRun: ArmId,
            AggregateStatus: ValidationStatus.Healthy,
            CheckResults: Enum.GetValues<ValidationCheckName>().Select(n => new ValidationCheckResult(
                Name: n,
                Outcome: ValidationCheckOutcome.Pass,
                Reason: "OK",
                ReasonCategory: ValidationFailureCategory.Ok,
                DurationMs: 10,
                CorrelationRequestId: null)).ToArray(),
            ArmResourceSnapshot: null,
            DriftDetected: false,
            DriftFields: Array.Empty<DriftField>(),
            TotalDurationMs: 50);
        await _factory.RunStore.AppendAsync(run, CancellationToken.None);

        var entity = new RegistryNamespace(
            id: id,
            name: "orders-prod",
            environment: "dev",
            status: RegistryEntityStatus.Active,
            createdAtUtc: now,
            updatedAtUtc: now,
            source: RegistrySource.Onboarded,
            fullyQualifiedName: "orders-prod",
            description: "Initial",
            tags: null,
            owner: null,
            azureResourceId: ArmId,
            metadata: null,
            etag: null)
        {
            DisplayName = "Orders Prod",
            SubscriptionId = subId,
            ResourceGroup = "rg-payments-prod",
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Region = "eastus2",
            BusinessUnit = "Payments",
            LifecycleStatus = lifecycle,
            ValidationStatus = ValidationStatus.Healthy,
            LastValidationRunId = runId,
            LastValidatedAtUtc = now,
            Ownership = new OwnershipBlock(BuildPrimaryOwner("Jane")),
            OnboardingActor = new OnboardingActor(Guid.NewGuid(), "actor", now),
        };

        var created = (RegistryNamespace)await _factory.EntityStore.CreateAsync(entity, CancellationToken.None);
        return (id, created.Etag!, created);
    }

    private static OwnershipAssignment BuildPrimaryOwner(string name) => new(
        Role: OwnershipRole.PrimaryOwner,
        PrincipalType: PrincipalType.User,
        ObjectId: Guid.NewGuid(),
        DisplayNameSnapshot: name,
        AssignedAtUtc: DateTimeOffset.UtcNow,
        AssignedBy: Guid.NewGuid());

    private static object BuildOwnership(string primaryName = "Jane", int stewards = 0) => new
    {
        primaryOwner = new
        {
            role = "PrimaryOwner",
            principalType = "User",
            objectId = Guid.NewGuid(),
            displayNameSnapshot = primaryName,
            assignedAtUtc = DateTimeOffset.UtcNow,
            assignedBy = Guid.NewGuid(),
        },
        secondaryOwners = Array.Empty<object>(),
        technicalStewards = Enumerable.Range(0, stewards).Select(i => new
        {
            role = "TechnicalSteward",
            principalType = "User",
            objectId = Guid.NewGuid(),
            displayNameSnapshot = $"Steward {i}",
            assignedAtUtc = DateTimeOffset.UtcNow,
            assignedBy = Guid.NewGuid(),
        }).ToArray(),
        supportContacts = Array.Empty<object>(),
    };

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
