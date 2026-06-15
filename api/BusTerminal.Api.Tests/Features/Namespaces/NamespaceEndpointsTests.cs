using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Namespaces;

// Spec 008 / T061–T064. Consolidated endpoint contract tests for the four
// wizard-supporting endpoints:
//   - GET /api/namespaces/identity  (T063)
//   - GET /api/namespaces/_picker   (T064)
//   - POST /api/namespaces/_validate (T061)
//   - POST /api/namespaces          (T062)
public sealed class NamespaceEndpointsTests : IClassFixture<NamespacesContractFactory>
{
    private const string ValidArmId =
        "/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg-payments-prod/providers/Microsoft.ServiceBus/namespaces/orders-prod";

    private readonly NamespacesContractFactory _factory;

    public NamespaceEndpointsTests(NamespacesContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
        // Reset probe outcomes between tests.
        _factory.ArmProbe.ExistenceOutcome = global::BusTerminal.Api.Features.Namespaces.Shared.ValidationCheckOutcome.Pass;
        _factory.ArmProbe.AccessibilityOutcome = global::BusTerminal.Api.Features.Namespaces.Shared.ValidationCheckOutcome.Pass;
        _factory.ArmProbe.RequiredPermissionsOutcome = global::BusTerminal.Api.Features.Namespaces.Shared.ValidationCheckOutcome.Pass;
        _factory.ArmProbe.IdentityAuthorizationOutcome = global::BusTerminal.Api.Features.Namespaces.Shared.ValidationCheckOutcome.Pass;
        _factory.ArmProbe.ApiReachabilityOutcome = global::BusTerminal.Api.Features.Namespaces.Shared.ValidationCheckOutcome.Pass;
    }

    // === GET /api/namespaces/identity ===

    [Fact]
    public async Task GetIdentity_Authenticated_ReturnsPrincipalIdAndCommand()
    {
        using var client = _factory.CreateClient();
        AttachReaderHeaders(client);

        var response = await client.GetAsync("/api/namespaces/identity");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await ReadJson(response);
        doc.GetProperty("principalId").GetGuid().Should().Be(NamespacesContractFactory.WorkloadPrincipalId);
        doc.GetProperty("sampleGrantCommand").GetString().Should().Contain("az role assignment create");
        doc.GetProperty("runbookUrl").GetString().Should().NotBeNullOrEmpty();
    }

    // === GET /api/namespaces/_picker ===

    [Fact]
    public async Task GetPicker_Authenticated_ReturnsItems()
    {
        _factory.GraphPicker.Items.Clear();
        _factory.GraphPicker.Items.Add(new PrincipalPickerItem(
            ObjectId: Guid.NewGuid(),
            PrincipalType: PrincipalType.User,
            DisplayName: "Alice",
            Mail: "alice@busterminal.local",
            UserPrincipalName: "alice@busterminal.local"));

        using var client = _factory.CreateClient();
        AttachReaderHeaders(client);

        var response = await client.GetAsync("/api/namespaces/_picker?q=Al");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await ReadJson(response);
        var items = doc.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("displayName").GetString().Should().Be("Alice");
    }

    [Fact]
    public async Task GetPicker_EmptyQuery_Returns400()
    {
        using var client = _factory.CreateClient();
        AttachReaderHeaders(client);

        var response = await client.GetAsync("/api/namespaces/_picker?q=");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // === POST /api/namespaces/_validate ===

    [Fact]
    public async Task PostValidate_HappyPath_Returns201_PersistsRun()
    {
        using var client = _factory.CreateClient();
        AttachReaderHeaders(client);

        var proposedId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/namespaces/_validate", new
        {
            azureResourceId = ValidArmId,
            proposedNamespaceId = proposedId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await ReadJson(response);
        doc.GetProperty("aggregateStatus").GetString().Should().Be("Healthy");
        doc.GetProperty("namespaceId").GetGuid().Should().Be(proposedId);
        doc.GetProperty("checkResults").GetArrayLength().Should().Be(5);
        _factory.RunStore.All().Should().ContainSingle(r => r.NamespaceId == proposedId);
    }

    [Fact]
    public async Task PostValidate_MalformedArmId_Returns400()
    {
        using var client = _factory.CreateClient();
        AttachReaderHeaders(client);

        var response = await client.PostAsJsonAsync("/api/namespaces/_validate", new
        {
            azureResourceId = "not-an-arm-id",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostValidate_UnhealthyChecks_StillPersistsRun()
    {
        _factory.ArmProbe.ExistenceOutcome = global::BusTerminal.Api.Features.Namespaces.Shared.ValidationCheckOutcome.Fail;

        using var client = _factory.CreateClient();
        AttachReaderHeaders(client);

        var response = await client.PostAsJsonAsync("/api/namespaces/_validate", new
        {
            azureResourceId = ValidArmId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await ReadJson(response);
        doc.GetProperty("aggregateStatus").GetString().Should().Be("Unhealthy");
        _factory.RunStore.All().Should().NotBeEmpty();
    }

    // === POST /api/namespaces (register) ===

    [Fact]
    public async Task PostRegister_NonAdmin_Returns403()
    {
        using var client = _factory.CreateClient();
        AttachReaderHeaders(client);

        var response = await client.PostAsJsonAsync("/api/namespaces", new
        {
            id = Guid.NewGuid(),
            azureResourceId = ValidArmId,
            displayName = "Orders",
            environment = "dev",
            validationRunId = Guid.NewGuid(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostRegister_HappyPath_Returns201_AndEmitsAudit()
    {
        // 1) Run validation to pre-allocate the ValidationRun with namespaceId = proposed.
        using var validateClient = _factory.CreateClient();
        AttachReaderHeaders(validateClient);
        var namespaceId = Guid.NewGuid();
        var validate = await validateClient.PostAsJsonAsync("/api/namespaces/_validate", new
        {
            azureResourceId = ValidArmId,
            proposedNamespaceId = namespaceId,
        });
        validate.StatusCode.Should().Be(HttpStatusCode.Created);
        var validateDoc = await ReadJson(validate);
        var runId = validateDoc.GetProperty("id").GetGuid();

        // 2) Register the namespace as a NamespaceAdministrator.
        using var registerClient = _factory.CreateClient();
        AttachAdminHeaders(registerClient);
        var register = await registerClient.PostAsJsonAsync("/api/namespaces", new
        {
            id = namespaceId,
            azureResourceId = ValidArmId,
            displayName = "Orders Prod",
            environment = "dev",
            description = "Orders messaging",
            businessUnit = "Payments",
            ownership = new
            {
                primaryOwner = new
                {
                    role = "PrimaryOwner",
                    principalType = "User",
                    objectId = Guid.NewGuid(),
                    displayNameSnapshot = "Jane Operator",
                    assignedAtUtc = DateTimeOffset.UtcNow,
                    assignedBy = Guid.NewGuid(),
                },
                secondaryOwners = Array.Empty<object>(),
                technicalStewards = Array.Empty<object>(),
                supportContacts = Array.Empty<object>(),
            },
            validationRunId = runId,
        });

        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var registered = await ReadJson(register);
        registered.GetProperty("id").GetGuid().Should().Be(namespaceId);
        registered.GetProperty("source").GetString().Should().Be("Onboarded");
        registered.GetProperty("lifecycleStatus").GetString().Should().Be("Active");
        registered.GetProperty("validationStatus").GetString().Should().Be("Healthy");

        // 3) Audit event emitted.
        _factory.AuditStore.All()
            .Should().ContainSingle(a => a.EntityId == namespaceId
                && a.EventType == BusTerminal.Api.Features.Registry.Shared.AuditEventType.NamespaceOnboarded);
    }

    [Fact]
    public async Task PostRegister_UnhealthyValidationRun_Returns409()
    {
        // First run a validation that's Unhealthy.
        _factory.ArmProbe.ExistenceOutcome = global::BusTerminal.Api.Features.Namespaces.Shared.ValidationCheckOutcome.Fail;
        using var validateClient = _factory.CreateClient();
        AttachReaderHeaders(validateClient);
        var namespaceId = Guid.NewGuid();
        var validate = await validateClient.PostAsJsonAsync("/api/namespaces/_validate", new
        {
            azureResourceId = ValidArmId,
            proposedNamespaceId = namespaceId,
        });
        var validateDoc = await ReadJson(validate);
        var runId = validateDoc.GetProperty("id").GetGuid();

        // Register hard-blocks per FR-023a.
        using var registerClient = _factory.CreateClient();
        AttachAdminHeaders(registerClient);
        var register = await registerClient.PostAsJsonAsync("/api/namespaces", new
        {
            id = namespaceId,
            azureResourceId = ValidArmId,
            displayName = "Orders",
            environment = "dev",
            ownership = NewOwnership(),
            validationRunId = runId,
        });

        register.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // === helpers ===

    private static void AttachReaderHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(MockAuthenticationHandler.MockRolesHeader, "BusTerminal.Reader");
    }

    private static void AttachAdminHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(MockAuthenticationHandler.MockRolesHeader, "BusTerminal.NamespaceAdministrator");
    }

    private static object NewOwnership() => new
    {
        primaryOwner = new
        {
            role = "PrimaryOwner",
            principalType = "User",
            objectId = Guid.NewGuid(),
            displayNameSnapshot = "Jane",
            assignedAtUtc = DateTimeOffset.UtcNow,
            assignedBy = Guid.NewGuid(),
        },
        secondaryOwners = Array.Empty<object>(),
        technicalStewards = Array.Empty<object>(),
        supportContacts = Array.Empty<object>(),
    };

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content).RootElement.Clone();
    }
}
