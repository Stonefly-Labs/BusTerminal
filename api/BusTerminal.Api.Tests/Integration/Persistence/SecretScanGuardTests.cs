using System.Text;
using System.Text.Json;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Relationships;
using FluentAssertions;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T151 / SC-011 evidence.
//
// Loads the shipped fixture set into the emulator, then walks every persisted
// document in both the `resources` and `change-events` containers. Each
// document body is scanned (case-insensitive substring match) against the
// denylist enumerated in plan.md § Constraints:
//
//   password, secret, connectionstring, accesskey, sastoken, bearer, apikey
//
// Any hit fails the test with the container, document id, and matched term so
// the offending fixture or model field is quickly identifiable. The scan uses
// the Cosmos stream-query API and parses the raw HTTP body — so the assertion
// reflects exactly what landed in Cosmos, not what the .NET model object
// looked like in memory. (A denylisted term added to a future field or
// extension surface that is filtered out by the serializer would not be
// persisted; we want the test to be persistence-faithful, not model-shaped.)
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class SecretScanGuardTests
{
    private readonly CosmosEmulatorFixture _fixture;

    public SecretScanGuardTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    private static readonly string[] Denylist =
    {
        "password",
        "secret",
        "connectionstring",
        "accesskey",
        "sastoken",
        "bearer",
        "apikey",
    };

    private static string FixturePath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", file);

    [Fact]
    public async Task Persisted_documents_contain_no_credential_shaped_substrings()
    {
        await TruncateAsync();
        await LoadAllFixturesAsync();

        var findings = new List<string>();
        await ScanContainerAsync(_fixture.ResourcesCosmosContainer, "resources", findings);
        await ScanContainerAsync(_fixture.ChangeEventsCosmosContainer, "change-events", findings);

        findings.Should().BeEmpty(
            "no persisted document body may contain credential-shaped substrings (plan.md § Constraints / SC-011). "
            + "Offenders:\n  - "
            + string.Join("\n  - ", findings));
    }

    private static async Task ScanContainerAsync(Container container, string containerName, List<string> findings)
    {
        using var iterator = container.GetItemQueryStreamIterator("SELECT * FROM c");
        while (iterator.HasMoreResults)
        {
            using var response = await iterator.ReadNextAsync();
            response.IsSuccessStatusCode.Should().BeTrue(
                $"stream query against {containerName} must succeed");

            using var reader = new StreamReader(response.Content, Encoding.UTF8, leaveOpen: false);
            var body = await reader.ReadToEndAsync();

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("Documents", out var documents))
            {
                continue;
            }

            foreach (var item in documents.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "<no-id>" : "<no-id>";
                var raw = item.GetRawText();
                foreach (var term in Denylist)
                {
                    var idx = raw.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        var start = Math.Max(0, idx - 20);
                        var len = Math.Min(raw.Length - start, term.Length + 40);
                        var snippet = raw.Substring(start, len);
                        findings.Add($"[{containerName}/{id}] matched '{term}' near: {snippet}");
                    }
                }
            }
        }
    }

    private async Task LoadAllFixturesAsync()
    {
        var files = new[]
        {
            "01-base.json",
            "02-relationships.json",
            "03-contracts.json",
            "04-extensions.json",
            "05-environments.json",
        };

        foreach (var name in files)
        {
            var path = FixturePath(name);
            if (!File.Exists(path))
            {
                continue;
            }

            var envelope = _fixture.Serializer.DeserializeEnvelopeFromJson(await File.ReadAllTextAsync(path));

            foreach (var resource in envelope.Resources)
            {
                var existing = await _fixture.Store.GetAsync(
                    resource.Id,
                    resource.ResourceType,
                    includeDeleted: true,
                    default);

                if (existing is null)
                {
                    await _fixture.Store.CreateAsync(resource, TestActor, "integration-test", default);
                }
                else
                {
                    var patched = resource with { ConcurrencyToken = existing.ConcurrencyToken };
                    await _fixture.Store.UpdateAsync(patched, TestActor, "integration-test", default);
                }
            }

            foreach (var relationship in envelope.Relationships)
            {
                var existing = await _fixture.Store.GetRelationshipAsync(relationship.Id, includeDeleted: true, default);
                if (existing is null)
                {
                    await _fixture.Store.CreateRelationshipAsync(relationship, TestActor, "integration-test", default);
                }
            }
        }
    }

    private async Task TruncateAsync()
    {
        await DrainAsync(_fixture.ResourcesCosmosContainer, "resourceType");
        await DrainAsync(_fixture.ChangeEventsCosmosContainer, "resourceId");
    }

    private static async Task DrainAsync(Container container, string partitionKeyField)
    {
        var query = $"SELECT c.id, c[\"{partitionKeyField}\"] AS pk FROM c";
        using var iterator = container.GetItemQueryIterator<DocumentRef>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            foreach (var d in page)
            {
                if (d.Pk is null || d.Id is null)
                {
                    continue;
                }

                await container.DeleteItemAsync<object>(
                    d.Id,
                    new PartitionKey(d.Pk));
            }
        }
    }

    private sealed record DocumentRef(string? Id, string? Pk);
}
