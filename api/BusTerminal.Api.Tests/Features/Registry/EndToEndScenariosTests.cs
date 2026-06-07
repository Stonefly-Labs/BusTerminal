using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BusTerminal.Api.Features.Registry.Shared;
using BusTerminal.Api.Tests.Features.Registry.Fakes;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Features.Registry;

// Spec 006 / T131 [TEST]. Cross-story integration coverage of the three
// quickstart walkthroughs (US1 golden-path CRUD + browse, US2 search, US3
// relationships + audit) in a single fixture so a single test run gates the
// full slice. The intent is end-of-phase regression coverage — per-story
// detail lives in CreateEntityEndpointTests, SearchEndpointTests,
// AuditEndpointTests, etc.
public sealed class EndToEndScenariosTests : IClassFixture<RegistryContractFactory>
{
    private readonly RegistryContractFactory _factory;

    public EndToEndScenariosTests(RegistryContractFactory factory)
    {
        _factory = factory;
        _factory.EntityStore.Clear();
        _factory.AuditStore.Clear();
        _factory.SearchClient.Reset();
    }

    [Fact]
    public async Task QuickstartScenarios_AllThreeUserStories_PassInOneRun()
    {
        using var client = _factory.CreateClient();

        // -------------------------------------------------------------------
        // US1 — quickstart §5: register the canonical hierarchy
        // (Namespace → Topic → Subscription → Rule plus a sibling Queue),
        // browse via the list endpoint, and read each entity's detail.
        // -------------------------------------------------------------------
        var nsId = Guid.NewGuid();
        var nsResp = await client.PostAsJsonAsync("/api/registry", new
        {
            id = nsId,
            entityType = "Namespace",
            name = "orders-prod",
            environment = "dev",
            status = "Active",
            source = "Manual",
            owner = "payments-platform",
            tags = new[] { new { key = "Owner", value = "PaymentsTeam" } },
        });
        nsResp.StatusCode.Should().Be(HttpStatusCode.Created, "US1 namespace create must succeed");
        var nsEtag = nsResp.Headers.ETag!.Tag;

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

        // Browse via the env-scoped list endpoint — every entity we created
        // must be visible. Per amended FR-035 the env query parameter is
        // required; the list MUST exclude tombstoned documents.
        var listResp = await client.GetAsync("/api/registry?environment=dev&pageSize=100");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await listResp.Content.ReadAsStringAsync();
        using (var listDoc = JsonDocument.Parse(listJson))
        {
            var ids = listDoc.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .Select(e => e.GetProperty("id").GetGuid())
                .ToHashSet();
            ids.Should().Contain(new[] { nsId, topicId, queueId, subId, ruleId });
        }

        // Detail page parity — every entity is readable by id.
        foreach (var id in new[] { nsId, topicId, queueId, subId, ruleId })
        {
            (await client.GetAsync($"/api/registry/{id}")).StatusCode
                .Should().Be(HttpStatusCode.OK);
        }

        // Update the namespace's description — this exercises the
        // ETag-guarded write path and produces a second audit event.
        using (var put = new HttpRequestMessage(HttpMethod.Put, $"/api/registry/{nsId}")
        {
            Content = JsonContent.Create(new
            {
                id = nsId,
                entityType = "Namespace",
                name = "orders-prod",
                environment = "dev",
                status = "Active",
                source = "Manual",
                owner = "payments-platform",
                description = "Primary production orders namespace",
                tags = new[] { new { key = "Owner", value = "PaymentsTeam" } },
            }),
        })
        {
            put.Headers.Add("If-Match", nsEtag);
            var putResp = await client.SendAsync(put);
            putResp.StatusCode.Should().Be(HttpStatusCode.OK, "US1 namespace edit must succeed");
        }

        // -------------------------------------------------------------------
        // US2 — quickstart §6: the search endpoint returns ranked items the
        // adapter produces, and the request fan-out (filter/sort/pagination)
        // reaches the adapter with the operator's intent intact.
        // -------------------------------------------------------------------
        _factory.SearchClient.NextResults = new RegistrySearchResults(
            Hits: new[]
            {
                new RegistrySearchHit(
                    Id: queueId,
                    EntityType: RegistryEntityType.Queue,
                    Name: "orders-incoming",
                    FullyQualifiedName: "orders-prod/orders-incoming",
                    Environment: "dev",
                    Status: "Active",
                    Owner: "payments-platform",
                    NamespaceName: "orders-prod",
                    ParentId: nsId,
                    Score: 2.71),
            },
            TotalCount: 1);

        var searchResp = await client.GetAsync(
            "/api/registry/search?q=orders&entityType=Queue&environment=dev&status=Active&page=1&pageSize=25");
        searchResp.StatusCode.Should().Be(HttpStatusCode.OK, "US2 happy-path search must succeed");
        var searchJson = await searchResp.Content.ReadAsStringAsync();
        using (var searchDoc = JsonDocument.Parse(searchJson))
        {
            searchDoc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
            var first = searchDoc.RootElement.GetProperty("items")[0];
            first.GetProperty("name").GetString().Should().Be("orders-incoming");
        }

        var lastRequest = _factory.SearchClient.LastRequest;
        lastRequest.Should().NotBeNull("the endpoint must forward the parsed request to the adapter");
        lastRequest!.EntityTypeFilter.Should().Be(RegistryEntityType.Queue);
        lastRequest.EnvironmentFilter.Should().Be("dev");
        lastRequest.StatusFilter.Should().Be(RegistryEntityStatus.Active);
        lastRequest.Top.Should().Be(25);

        // -------------------------------------------------------------------
        // US3 — quickstart §7: relationships traversal + per-entity audit.
        // The list endpoint with `parentId=` gives the children panel its
        // data; the audit endpoint gives the audit panel its data with
        // newest-first ordering and append-only semantics.
        // -------------------------------------------------------------------
        var childrenResp = await client.GetAsync(
            $"/api/registry?environment=dev&parentId={nsId}&pageSize=100");
        childrenResp.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var childrenDoc = JsonDocument.Parse(await childrenResp.Content.ReadAsStringAsync()))
        {
            var childIds = childrenDoc.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .Select(e => e.GetProperty("id").GetGuid())
                .ToHashSet();
            childIds.Should().Contain(new[] { topicId, queueId });
        }

        var auditResp = await client.GetAsync($"/api/registry/{nsId}/audit");
        auditResp.StatusCode.Should().Be(HttpStatusCode.OK, "US3 audit list must succeed");
        using (var auditDoc = JsonDocument.Parse(await auditResp.Content.ReadAsStringAsync()))
        {
            var items = auditDoc.RootElement.GetProperty("items");
            items.GetArrayLength().Should().BeGreaterThanOrEqualTo(2,
                "create + edit must each emit an audit event");

            // Newest first — the edit must precede the create.
            var newest = items[0];
            var oldest = items[items.GetArrayLength() - 1];
            newest.GetProperty("eventType").GetString().Should().Be("Updated");
            oldest.GetProperty("eventType").GetString().Should().Be("Created");

            // Every audit event carries the registry actor + correlationId
            // shape required by contracts/audit-event.schema.json.
            foreach (var item in items.EnumerateArray())
            {
                item.GetProperty("actor").GetProperty("principalId").GetString()
                    .Should().NotBeNullOrEmpty();
                item.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
            }
        }

        // FR-034 — write verbs MUST NOT be exposed on the audit route.
        foreach (var verb in new[] { "POST", "PUT", "DELETE", "PATCH" })
        {
            using var req = new HttpRequestMessage(new HttpMethod(verb), $"/api/registry/{nsId}/audit")
            {
                Content = JsonContent.Create(new { dummy = true }),
            };
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should()
                .BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound);
        }
    }
}
