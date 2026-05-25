using System.Net;
using System.Text.Json;
using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Relationships;
using BusTerminal.Api.Domain.Serialization;
using BusTerminal.Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / FR-016 / T147 / SC-009 final evidence.
//
// Load representative fixtures into the emulator, export the canonical state to
// JSON (and YAML), wipe, re-import, and assert the load-bearing metadata
// survives the round-trip — identifiers, types, names, namespace paths,
// lifecycle, versions, ownership, extensions, environments, and per-type fields
// (relationships preserve direction + type + annotations; soft-delete state is
// preserved when --include-deleted was used during export).
//
// "Byte-meaningful equivalence" per the spec's Independent Test: the audit and
// concurrency-token fields are deliberately NOT included in the comparison —
// re-import is a fresh write, so timestamps + ETags differ by design. The
// canonical store stamps audit metadata; that is the desired behavior (the
// re-imported record reflects "imported by tools/load-fixtures at <now>", which
// is the audit trail Scenario 2 wants).
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class ExportImportRoundTripTests
{
    private readonly CosmosEmulatorFixture _fixture;

    public ExportImportRoundTripTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    private static string FixturePath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", file);

    [Fact]
    public async Task Json_envelope_round_trips_resources_relationships_extensions_lifecycle()
    {
        await TruncateAsync();
        await LoadAllFixturesAsync();

        var originalEnvelope = await ExportEnvelopeAsync(includeDeleted: true);

        var json = _fixture.Serializer.SerializeEnvelopeToJson(originalEnvelope);
        var rehydrated = _fixture.Serializer.DeserializeEnvelopeFromJson(json);

        await TruncateAsync();
        await ReimportEnvelopeAsync(rehydrated);

        var reExported = await ExportEnvelopeAsync(includeDeleted: true);

        AssertEnvelopesAreEquivalent(originalEnvelope, reExported);
    }

    [Fact]
    public async Task Yaml_envelope_round_trips_resources_relationships_extensions_lifecycle()
    {
        await TruncateAsync();
        await LoadAllFixturesAsync();

        var originalEnvelope = await ExportEnvelopeAsync(includeDeleted: true);

        var yaml = _fixture.YamlSerializer().SerializeEnvelopeToYaml(originalEnvelope);
        yaml.Should().NotBeNullOrWhiteSpace();
        var rehydrated = _fixture.YamlSerializer().DeserializeEnvelopeFromYaml(yaml);

        await TruncateAsync();
        await ReimportEnvelopeAsync(rehydrated);

        var reExported = await ExportEnvelopeAsync(includeDeleted: true);

        AssertEnvelopesAreEquivalent(originalEnvelope, reExported);
    }

    [Fact]
    public async Task Conflict_resolution_reject_against_non_empty_store_surfaces_structured_failure()
    {
        await TruncateAsync();
        await LoadAllFixturesAsync();

        // Snapshot the populated state, then attempt to re-import the same
        // envelope into a still-populated store. The persistence-layer 409
        // CONFLICT is the structured failure that the load-fixtures CLI maps to
        // its [conflict-rejected] line under --conflict-resolution=reject.
        var envelope = await ExportEnvelopeAsync(includeDeleted: true);

        var conflictHits = 0;
        foreach (var resource in envelope.Resources.Take(3))
        {
            try
            {
                await _fixture.Store.CreateAsync(resource, TestActor, "integration-test", default);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                conflictHits++;
            }
        }

        conflictHits.Should().Be(3, "every duplicate-identifier CreateAsync must produce a Conflict response under reject semantics");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task LoadAllFixturesAsync()
    {
        // The shipped fixture set is multi-file with patch-style overlays
        // (T134 / T138). Load them in lexicographic order so later files
        // overlay earlier ones via Update.
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

    private async Task<ImportExportEnvelope> ExportEnvelopeAsync(bool includeDeleted)
    {
        var resources = new List<Resource>();
        await foreach (var r in _fixture.Store.QueryAsync(
            new ResourceQuery.All(ResourceTypeDiscriminator: null, IncludeDeleted: includeDeleted),
            default))
        {
            resources.Add(r);
        }

        var relationships = new List<Relationship>();
        await foreach (var rel in _fixture.Store.QueryRelationshipsAsync(
            new RelationshipQuery.All(IncludeDeleted: includeDeleted),
            default))
        {
            relationships.Add(rel);
        }

        return new ImportExportEnvelope(
            ExportedAt: DateTimeOffset.UtcNow,
            Resources: resources,
            Relationships: relationships,
            ExportedBy: TestActor,
            SourceSystem: "integration-test");
    }

    private async Task ReimportEnvelopeAsync(ImportExportEnvelope envelope)
    {
        foreach (var resource in envelope.Resources)
        {
            await _fixture.Store.CreateAsync(resource, TestActor, "integration-test", default);
        }

        foreach (var relationship in envelope.Relationships)
        {
            await _fixture.Store.CreateRelationshipAsync(relationship, TestActor, "integration-test", default);
        }
    }

    private void AssertEnvelopesAreEquivalent(
        ImportExportEnvelope original,
        ImportExportEnvelope reExported)
    {
        // Resource set equivalence — same IDs, same per-resource normalized
        // JSON (Audit + ConcurrencyToken + ValidationState stripped because
        // those legitimately differ across writes).
        var originalById = original.Resources.ToDictionary(r => r.Id, NormalizeResource);
        var reExportedById = reExported.Resources.ToDictionary(r => r.Id, NormalizeResource);

        reExportedById.Keys.Should().BeEquivalentTo(originalById.Keys,
            "every resource ID must survive the round-trip");

        foreach (var (id, originalJson) in originalById)
        {
            reExportedById[id].Should().Be(originalJson,
                $"resource {id} must round-trip the load-bearing fields");
        }

        // Relationship set equivalence — same IDs, same normalized JSON
        // (Audit + ConcurrencyToken stripped). Direction is preserved because
        // SourceId/TargetId are part of the comparison; type because Type is.
        var originalRel = original.Relationships.ToDictionary(r => r.Id, NormalizeRelationship);
        var reExportedRel = reExported.Relationships.ToDictionary(r => r.Id, NormalizeRelationship);

        reExportedRel.Keys.Should().BeEquivalentTo(originalRel.Keys,
            "every relationship ID must survive the round-trip");

        foreach (var (id, originalJson) in originalRel)
        {
            reExportedRel[id].Should().Be(originalJson,
                $"relationship {id} must round-trip direction + type + annotations");
        }
    }

    private string NormalizeResource(Resource resource)
    {
        // Strip the per-write fields so the comparison reflects load-bearing
        // metadata only. Spec §183 "byte-meaningful equivalence".
        var stripped = resource with
        {
            ConcurrencyToken = ConcurrencyToken.Empty,
            ValidationState = null,
            Audit = resource.Audit with
            {
                CreatedAt = DateTimeOffset.MinValue,
                ModifiedAt = DateTimeOffset.MinValue,
                ModifiedBy = TestActor,
                CreatedBy = TestActor,
                SourceSystem = null,
            },
        };

        return _fixture.Serializer.SerializeToJson(stripped);
    }

    private string NormalizeRelationship(Relationship relationship)
    {
        var stripped = relationship with
        {
            ConcurrencyToken = ConcurrencyToken.Empty,
            ValidationState = null,
            Audit = relationship.Audit with
            {
                CreatedAt = DateTimeOffset.MinValue,
                ModifiedAt = DateTimeOffset.MinValue,
                ModifiedBy = TestActor,
                CreatedBy = TestActor,
                SourceSystem = null,
            },
        };

        return JsonSerializer.Serialize(stripped, _fixture.Serializer.Options);
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
            foreach (var doc in page)
            {
                if (doc.Pk is null || doc.Id is null)
                {
                    continue;
                }

                await container.DeleteItemAsync<object>(
                    doc.Id,
                    new PartitionKey(doc.Pk));
            }
        }
    }

    private sealed record DocumentRef(string? Id, string? Pk);
}

internal static class CosmosEmulatorFixtureYamlExtensions
{
    // The fixture exposes the JSON serializer directly; the YAML serializer is
    // resolved via the shared service provider so test code stays mechanism-
    // agnostic about which DI registration is the "default" IResourceSerializer.
    public static YamlResourceSerializer YamlSerializer(this CosmosEmulatorFixture fixture) =>
        fixture.Services.GetRequiredService<YamlResourceSerializer>();
}
