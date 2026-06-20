using System.Net;
using System.Net.Http;
using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.GetDiscoveryRun;

// Spec 009 / T032. Contract test for GET /api/discovery-runs/{id}?namespaceId=...
public sealed class GetDiscoveryRunContractTests : IClassFixture<DiscoveryContractFactory>
{
    private readonly DiscoveryContractFactory _factory;

    public GetDiscoveryRunContractTests(DiscoveryContractFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDiscoveryRun_ExistingRun_Returns200_AndExpectedFields()
    {
        var ns = "ns_test_get";
        var run = new DiscoveryRun(
            Id: "dr_TEST00000000000000000000001",
            SchemaVersion: DiscoveryRun.CurrentSchemaVersion,
            NamespaceId: ns,
            Status: DiscoveryRunStatus.Succeeded,
            Trigger: DiscoveryTrigger.Manual,
            StartedUtc: DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedUtc: DateTimeOffset.UtcNow,
            DurationMs: 12345,
            RequestedBy: "00000000-0000-0000-0000-000000000099",
            QueueCount: 5, TopicCount: 2, SubscriptionCount: 4, RuleCount: 6,
            NewCount: 3, UpdatedCount: 1, UnchangedCount: 12, MissingCount: 0,
            Failure: null,
            CoalescedRequests: Array.Empty<CoalescedRequest>(),
            CorrelationId: "00-test-trace-01");
        await _factory.RunStore.CreateAsync(run, CancellationToken.None);

        using var client = ReaderClient();
        var response = await client.GetAsync($"/api/discovery-runs/{run.Id}?namespaceId={ns}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJson(response);
        body.GetProperty("id").GetString().Should().Be(run.Id);
        body.GetProperty("status").GetString().Should().Be("Succeeded");
        body.GetProperty("durationMs").GetInt32().Should().Be(12345);
    }

    [Fact]
    public async Task GetDiscoveryRun_MissingNamespaceQuery_Returns400()
    {
        using var client = ReaderClient();
        var response = await client.GetAsync("/api/discovery-runs/dr_DOESNOTEXIST00000000000000");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDiscoveryRun_UnknownRun_Returns404()
    {
        using var client = ReaderClient();
        var response = await client.GetAsync("/api/discovery-runs/dr_UNKNOWN0000000000000000000?namespaceId=ns_nope");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDiscoveryRun_NoRoleHeader_StillAllowed_BecauseAuthenticatedOnly()
    {
        // Read endpoints are AuthN-only — any authenticated user can read.
        // In Development the MockAuth handler authenticates the dev user even
        // without X-Mock-Roles, so this is a 404 (unknown run) not 401.
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/discovery-runs/dr_UNKNOWN?namespaceId=ns_x");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
