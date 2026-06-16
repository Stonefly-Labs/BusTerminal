using System.Net;
using System.Net.Http;
using System.Text.Json;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Namespaces.Details;

// Spec 008 / T096 + T103 / US2. Contract tests for GET /api/namespaces/{id}.
// Covers response shape (latestValidationRun joined from
// namespace-validation-runs, recentAuditEvents joined from registry-audit),
// 404 on missing id, AuthN-only read.
public sealed class DetailsEndpointTests : IClassFixture<NamespacesContractFactory>
{
    private readonly NamespacesContractFactory _factory;

    public DetailsEndpointTests(NamespacesContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
        // RunStore has no Clear surface; tests use unique ids so it doesn't matter.
    }

    [Fact]
    public async Task Get_MissingNamespace_Returns404()
    {
        using var client = AuthenticatedClient();

        var response = await client.GetAsync($"/api/namespaces/{Guid.NewGuid():D}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_OnboardedNamespace_ReturnsDetailsWithEtag()
    {
        var (id, _, _) = await SeedOnboardedAsync();

        using var client = AuthenticatedClient();
        var response = await client.GetAsync($"/api/namespaces/{id:D}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();

        var doc = await ReadJson(response);
        doc.GetProperty("id").GetGuid().Should().Be(id);
        doc.GetProperty("source").GetString().Should().Be("Onboarded");
        doc.GetProperty("displayName").GetString().Should().Be("Test Namespace");
        doc.GetProperty("lifecycleStatus").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Get_IncludesLatestValidationRun()
    {
        var (id, runId, _) = await SeedOnboardedAsync();

        using var client = AuthenticatedClient();
        var response = await client.GetAsync($"/api/namespaces/{id:D}");
        var doc = await ReadJson(response);

        doc.TryGetProperty("latestValidationRun", out var run).Should().BeTrue();
        run.GetProperty("id").GetGuid().Should().Be(runId);
        run.GetProperty("aggregateStatus").GetString().Should().Be("Healthy");
        run.GetProperty("checkResults").GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task Get_IncludesRecentAuditEvents_NewestFirst()
    {
        var (id, _, _) = await SeedOnboardedAsync();

        // Seed two audit events for the same entity.
        await _factory.AuditStore.WriteAsync(new AuditEvent(
            Id: Guid.NewGuid(),
            EntityId: id,
            EntityType: RegistryEntityType.Namespace,
            Environment: "dev",
            EventType: AuditEventType.NamespaceOnboarded,
            Timestamp: DateTimeOffset.UtcNow.AddMinutes(-10),
            Actor: new AuditActor(Guid.NewGuid().ToString("D"), "Jane"),
            ChangeSummary: "First event",
            WasForceOverwrite: false,
            CorrelationId: Guid.NewGuid().ToString("D")), CancellationToken.None);
        await _factory.AuditStore.WriteAsync(new AuditEvent(
            Id: Guid.NewGuid(),
            EntityId: id,
            EntityType: RegistryEntityType.Namespace,
            Environment: "dev",
            EventType: AuditEventType.NamespaceMetadataUpdated,
            Timestamp: DateTimeOffset.UtcNow,
            Actor: new AuditActor(Guid.NewGuid().ToString("D"), "Jane"),
            ChangeSummary: "Second event",
            WasForceOverwrite: false,
            CorrelationId: Guid.NewGuid().ToString("D")), CancellationToken.None);

        using var client = AuthenticatedClient();
        var response = await client.GetAsync($"/api/namespaces/{id:D}");
        var doc = await ReadJson(response);

        doc.TryGetProperty("recentAuditEvents", out var audit).Should().BeTrue();
        audit.GetArrayLength().Should().Be(2);
        audit[0].GetProperty("changeSummary").GetString().Should().Be("Second event");
    }

    // === helpers ===

    private async Task<(Guid Id, Guid RunId, RegistryNamespace Entity)> SeedOnboardedAsync()
    {
        var id = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var run = new ValidationRun(
            Id: runId,
            NamespaceId: id,
            ExecutedAtUtc: now,
            ExecutedBy: Guid.NewGuid(),
            ExecutedByDisplayNameSnapshot: "actor",
            AzureResourceIdAtRun: "/subscriptions/x/resourceGroups/y/providers/Microsoft.ServiceBus/namespaces/z",
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
            name: "test-ns",
            environment: "dev",
            status: RegistryEntityStatus.Active,
            createdAtUtc: now,
            updatedAtUtc: now,
            source: RegistrySource.Onboarded,
            fullyQualifiedName: "test-ns",
            description: "Test",
            tags: null,
            owner: null,
            azureResourceId: "/subscriptions/x/resourceGroups/y/providers/Microsoft.ServiceBus/namespaces/test-ns",
            metadata: null,
            etag: null)
        {
            DisplayName = "Test Namespace",
            SubscriptionId = Guid.NewGuid(),
            ResourceGroup = "rg",
            TenantId = Guid.NewGuid(),
            Region = "eastus2",
            LifecycleStatus = LifecycleStatus.Active,
            ValidationStatus = ValidationStatus.Healthy,
            LastValidationRunId = runId,
            LastValidatedAtUtc = now,
            Ownership = new OwnershipBlock(
                PrimaryOwner: new OwnershipAssignment(
                    Role: OwnershipRole.PrimaryOwner,
                    PrincipalType: PrincipalType.User,
                    ObjectId: Guid.NewGuid(),
                    DisplayNameSnapshot: "Jane",
                    AssignedAtUtc: now,
                    AssignedBy: Guid.NewGuid())),
            OnboardingActor = new OnboardingActor(
                ObjectId: Guid.NewGuid(),
                DisplayNameSnapshot: "actor",
                OnboardedAtUtc: now),
        };

        var created = await _factory.EntityStore.CreateAsync(entity, CancellationToken.None);
        return (id, runId, (RegistryNamespace)created);
    }

    private HttpClient AuthenticatedClient()
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
