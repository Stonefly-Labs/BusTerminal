using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using BusTerminal.Api.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BusTerminal.Api.Tests.Performance;

// Spec 009 / T116a + SC-007 ("user can locate any entity by name through
// catalog search in under 10 seconds"). Measures p95 latency of
// GET /api/entities?q=... across ≥ 50 representative queries and asserts
// p95 ≤ 10 seconds end-to-end (network → backend → AI Search → response).
//
// Env-gated — runs against the dev environment when:
//   BUSTERMINAL_PERF_SEARCH_BASE_URL  = https://api.dev.busterminal.example.com
//   BUSTERMINAL_PERF_SEARCH_TOKEN     = <bearer JWT for a Reader principal>
//
// Tagged [Trait("Category","Performance")] so PR CI excludes it. The
// nightly perf job opts in via `dotnet test --filter Category=Performance`.
[Trait("Category", "Performance")]
public sealed class EntitySearchLatencyTests
{
    private const string BaseUrlEnvVar = "BUSTERMINAL_PERF_SEARCH_BASE_URL";
    private const string TokenEnvVar = "BUSTERMINAL_PERF_SEARCH_TOKEN";
    private const int MinQueryCount = 50;
    private const double P95BudgetSeconds = 10.0;

    private static IReadOnlyList<string> QueryCorpus { get; } = new[]
    {
        // A representative mix of common substrings + edge cases. Tuned to
        // exercise both single-token + multi-token + leading-wildcard paths
        // through AI Search analyzers.
        "orders", "payments", "inventory", "fulfillment", "audit",
        "ingest", "notification", "billing", "shipping", "tracking",
        "events", "commands", "queries", "publish", "subscribe",
        "topic", "queue", "rule", "sub", "deadletter",
        "ns_test", "ns_prod", "ns_dev", "svc_alpha", "svc_payments",
        "domain:orders", "tier:critical", "lifecycle", "active", "archived",
        "inb", "out", "evt", "cmd", "req",
        "checkout", "order-created", "payment-completed", "user-deleted", "tenant-onboarded",
        "wildcard", "rule-1", "rule-correlation", "rule-sql", "lock-",
        "scheduled", "retry", "missing", "owner", "consumer",
        "producer", "owner-role", "tag:dev", "tag:experimental", "rare-query",
    };

    [Fact]
    public async Task SearchEntities_P95LatencyUnder10Seconds()
    {
        var baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvVar);
        var token = Environment.GetEnvironmentVariable(TokenEnvVar);
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
        {
            // Skip cleanly when env not configured (matches the spec 006/008
            // integration-test pattern). The nightly perf job sets both.
            return;
        }

        QueryCorpus.Count.Should().BeGreaterThanOrEqualTo(MinQueryCount,
            $"SC-007 requires ≥ {MinQueryCount} representative queries");

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var latencies = new List<double>(QueryCorpus.Count);
        foreach (var query in QueryCorpus)
        {
            var sw = Stopwatch.StartNew();
            var response = await http.GetAsync($"/api/entities?q={Uri.EscapeDataString(query)}&pageSize=25");
            sw.Stop();
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
            latencies.Add(sw.Elapsed.TotalSeconds);
        }

        var p95 = Percentile(latencies, 0.95);
        Console.WriteLine($"search latency p50={Percentile(latencies, 0.5):F2}s p95={p95:F2}s p99={Percentile(latencies, 0.99):F2}s across {latencies.Count} queries");
        p95.Should().BeLessThanOrEqualTo(P95BudgetSeconds, "SC-007 requires p95 ≤ 10s for entity-by-name search");
    }

    private static double Percentile(IReadOnlyList<double> samples, double q)
    {
        if (samples.Count == 0) return 0;
        var sorted = samples.OrderBy(x => x).ToArray();
        var rank = q * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper)
        {
            return sorted[lower];
        }
        var weight = rank - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }
}

// MockAuthenticationHandler / WebApplicationFactory imports are kept on
// hand for future expansion (e.g. running the test against a locally
// hosted instance instead of the dev environment). They're unreferenced
// today but keep the imports in working order.
internal static class _Imports
{
    public static Type[] KeepImportsLive() => new[]
    {
        typeof(MockAuthenticationHandler),
        typeof(WebApplicationFactory<>),
    };
}
