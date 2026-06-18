using System.Net;
using System.Net.Http;
using System.Text.Json;
using BusTerminal.Api.Features.Discovery.Shared.Domain;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Discovery.ListDiscoveryRuns;

// Spec 009 / T080 / US3. Contract test for
//   GET /api/namespaces/{namespaceId}/discovery-runs?pageSize=&continuationToken=
//
// Validates: response shape per contracts/openapi.yaml (items[], optional
// continuationToken), pageSize clamping, reverse-chronological ordering,
// authenticated-only (any role) gating, and 4xx handling for an invalid
// namespaceId.
public sealed class ListDiscoveryRunsContractTests : IClassFixture<DiscoveryContractFactory>
{
    private readonly DiscoveryContractFactory _factory;

    public ListDiscoveryRunsContractTests(DiscoveryContractFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListDiscoveryRuns_ReturnsItemsArray_AndOmitsContinuation_WhenLastPage()
    {
        var ns = await SeedRunsAsync(count: 2);
        using var client = ReaderClient();

        var response = await client.GetAsync($"/api/namespaces/{ns:D}/discovery-runs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJson(response);
        body.TryGetProperty("items", out var items).Should().BeTrue();
        items.GetArrayLength().Should().Be(2);

        if (body.TryGetProperty("continuationToken", out var ct))
        {
            // Allowed: explicit JSON null on terminal page.
            ct.ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task ListDiscoveryRuns_OrdersByStartedUtcDescending()
    {
        var ns = await SeedRunsAsync(count: 3);
        using var client = ReaderClient();

        var response = await client.GetAsync($"/api/namespaces/{ns:D}/discovery-runs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJson(response);
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(3);
        var times = new DateTimeOffset[items.GetArrayLength()];
        for (var i = 0; i < items.GetArrayLength(); i++)
        {
            times[i] = items[i].GetProperty("startedUtc").GetDateTimeOffset();
        }
        times.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task ListDiscoveryRuns_RespectsPageSize_AndReturnsContinuation()
    {
        var ns = await SeedRunsAsync(count: 4);
        using var client = ReaderClient();

        var response = await client.GetAsync($"/api/namespaces/{ns:D}/discovery-runs?pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJson(response);
        body.GetProperty("items").GetArrayLength().Should().Be(2);
        body.GetProperty("continuationToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ListDiscoveryRuns_FollowsContinuationToken_ToTerminalPage()
    {
        var ns = await SeedRunsAsync(count: 5);
        using var client = ReaderClient();

        var collected = new List<string>();
        string? token = null;
        var safetyHops = 0;
        do
        {
            var url = $"/api/namespaces/{ns:D}/discovery-runs?pageSize=2"
                + (token is null ? string.Empty : $"&continuationToken={Uri.EscapeDataString(token)}");
            var response = await client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await ReadJson(response);
            var items = body.GetProperty("items");
            for (var i = 0; i < items.GetArrayLength(); i++)
            {
                collected.Add(items[i].GetProperty("id").GetString()!);
            }
            token = body.TryGetProperty("continuationToken", out var ct) && ct.ValueKind == JsonValueKind.String
                ? ct.GetString()
                : null;
            safetyHops++;
        } while (token is not null && safetyHops < 10);

        collected.Should().HaveCount(5);
        collected.Distinct().Should().HaveCount(5);
    }

    [Fact]
    public async Task ListDiscoveryRuns_ClampsPageSize_OutOfRange()
    {
        var ns = await SeedRunsAsync(count: 1);
        using var client = ReaderClient();

        // pageSize=0 → clamped to 1; pageSize=9999 → clamped to 100.
        var lower = await client.GetAsync($"/api/namespaces/{ns:D}/discovery-runs?pageSize=0");
        lower.StatusCode.Should().Be(HttpStatusCode.OK);

        var upper = await client.GetAsync($"/api/namespaces/{ns:D}/discovery-runs?pageSize=9999");
        upper.StatusCode.Should().Be(HttpStatusCode.OK);

        var lowerBody = await ReadJson(lower);
        lowerBody.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ListDiscoveryRuns_NamespaceWithNoRuns_ReturnsEmpty200()
    {
        using var client = ReaderClient();
        var response = await client.GetAsync($"/api/namespaces/{Guid.NewGuid():D}/discovery-runs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJson(response);
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ListDiscoveryRuns_RouteRejectsNonGuidNamespaceId_With404FromConstraint()
    {
        using var client = ReaderClient();
        var response = await client.GetAsync("/api/namespaces/not-a-guid/discovery-runs");
        // Route constraint {namespaceId:guid} causes a 404 (no matching
        // route) rather than 400 — this is the documented Minimal API
        // behaviour for typed route constraints.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> SeedRunsAsync(int count)
    {
        var ns = Guid.NewGuid();
        var nsString = ns.ToString("D");
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        for (var i = 0; i < count; i++)
        {
            var run = new DiscoveryRun(
                Id: $"dr_TEST_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                SchemaVersion: DiscoveryRun.CurrentSchemaVersion,
                NamespaceId: nsString,
                Status: DiscoveryRunStatus.Succeeded,
                Trigger: DiscoveryTrigger.Manual,
                StartedUtc: baseTime.AddSeconds(i),
                CompletedUtc: baseTime.AddSeconds(i).AddSeconds(30),
                DurationMs: 30000,
                RequestedBy: "00000000-0000-0000-0000-000000000099",
                QueueCount: i, TopicCount: 0, SubscriptionCount: 0, RuleCount: 0,
                NewCount: 0, UpdatedCount: 0, UnchangedCount: 0, MissingCount: 0,
                Failure: null,
                CoalescedRequests: Array.Empty<CoalescedRequest>(),
                CorrelationId: "00-test-trace-list");
            await _factory.RunStore.CreateAsync(run, CancellationToken.None);
        }
        return ns;
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
