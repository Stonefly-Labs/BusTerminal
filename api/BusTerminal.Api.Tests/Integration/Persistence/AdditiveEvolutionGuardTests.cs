using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Lifecycle;
using BusTerminal.Api.Domain.Relationships;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Validation;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T158 / SC-010 evidence — additive resource-type evolution.
//
// Scenario: a future build introduces a new resource type and persists documents
// carrying that discriminator. A sibling build (or a downgrade) that does NOT
// yet know about the type reads those documents. The guarantee Q4 makes is:
// existing documents are never migrated, never modified, never rejected — they
// materialize as UnknownResource with the original payload preserved on
// RawJson, and the UnknownResourceTypeRule emits an Info finding so operators
// can surface "we have N stored documents whose type isn't in the current
// build's registry."
//
// Flow:
//   1. Snapshot every fixture document's ETag (load-bearing: any drift on a
//      pre-existing doc would violate the "never migrated, never modified"
//      guarantee).
//   2. Register a synthetic discriminator on the SHARED ResourceTypeRegistry
//      via a try/finally scope — the registry is a singleton owned by the
//      collection fixture, so the synthetic registration MUST be reverted
//      before the test exits, including failure cases.
//   3. Persist a SyntheticForTestResource via the normal Store.CreateAsync
//      path. The write goes through the polymorphic serializer + the Cosmos
//      STJ serializer adapter — exactly the production path a future build
//      would take.
//   4. Unregister the synthetic discriminator — now the registry no longer
//      knows the type, matching the sibling/downgrade scenario.
//   5. Read the synthetic resource via Store.GetAsync. The polymorphic
//      converter's discriminator lookup returns false → falls through to the
//      UnknownResource factory → materializes as UnknownResource.RawJson with
//      the synthetic field intact.
//   6. Run the validation engine on the UnknownResource. UnknownResourceTypeRule
//      must emit one Info finding with the configured RuleId.
//   7. Re-snapshot every fixture document's ETag and assert byte-equivalence
//      with the step-1 snapshot.
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class AdditiveEvolutionGuardTests
{
    private const string SyntheticDiscriminator = "syntheticForTest";

    private readonly CosmosEmulatorFixture _fixture;

    public AdditiveEvolutionGuardTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    private static string FixturePath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", file);

    [Fact]
    public async Task Unknown_resourceType_round_trips_as_UnknownResource_without_modifying_existing_fixtures()
    {
        await TruncateAsync();
        await LoadAllFixturesAsync();

        var fixtureSnapshotBefore = await SnapshotFixturesAsync();
        fixtureSnapshotBefore.Should().NotBeEmpty(
            "fixture set must be loaded before exercising the additive-evolution scenario");

        var registry = _fixture.Services.GetRequiredService<ResourceTypeRegistry>();

        var syntheticId = ResourceId.New();
        var synthetic = BuildSyntheticResource(syntheticId);

        Resource? read = null;
        ValidationResult? validation = null;

        registry.Register(SyntheticDiscriminator, typeof(SyntheticForTestResource));
        try
        {
            await _fixture.Store.CreateAsync(synthetic, TestActor, "integration-test", default);
        }
        finally
        {
            // Unregister BEFORE the read so the converter's discriminator lookup
            // misses and falls through to the UnknownResource factory — that is
            // the simulated "future type unknown to current build" path.
            registry.Unregister(SyntheticDiscriminator).Should().BeTrue();
        }

        // Step 5 — read via the unregistered-type path.
        read = await _fixture.Store.GetAsync(syntheticId, SyntheticDiscriminator, includeDeleted: false, default);

        read.Should().NotBeNull("persisted document must remain readable after its type leaves the registry");
        read.Should().BeOfType<UnknownResource>(
            "documents with an unknown discriminator MUST materialize as UnknownResource (Q4 / FR-002)");

        var unknown = (UnknownResource)read!;
        unknown.ResourceType.Should().Be(SyntheticDiscriminator);
        unknown.Id.Should().Be(syntheticId);
        unknown.Name.Value.Should().Be(synthetic.Name.Value);

        // RawJson MUST preserve the synthetic-type-specific field (`syntheticPayload`)
        // verbatim — the additive-evolution guarantee depends on raw payload
        // preservation so a later build that re-registers the type can re-hydrate
        // every persisted field.
        unknown.RawJson.TryGetProperty("syntheticPayload", out var payload).Should().BeTrue(
            "the per-type synthetic field must survive the write/read round-trip on RawJson");
        payload.GetString().Should().Be("preserved-as-raw");

        // Step 6 — validate.
        using var scope = _fixture.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<ValidationEngine>();
        validation = await engine.ValidateAsync(
            unknown,
            relationshipResolver: _ => null,
            duplicateDetector: _ => false,
            previousLifecycle: null,
            default);

        validation.Findings.Should().Contain(
            f => f.RuleId == "unknown.resourceType" && f.Severity == ValidationSeverity.Info,
            "UnknownResourceTypeRule (T076) MUST emit exactly one Info finding so operators can surface unknown-type-document counts");

        validation.Findings
            .Count(f => f.RuleId == "unknown.resourceType")
            .Should().Be(1);

        // Step 7 — assert fixture documents are unchanged (no migration, no
        // modification). ETag drift is the load-bearing signal: any fixture
        // touched by the synthetic write/read path would have a new ETag.
        var fixtureSnapshotAfter = await SnapshotFixturesAsync();

        fixtureSnapshotAfter.Keys.Should().BeEquivalentTo(fixtureSnapshotBefore.Keys,
            "no fixture document was rejected or deleted by the synthetic-type write/read");

        foreach (var (key, etagBefore) in fixtureSnapshotBefore)
        {
            fixtureSnapshotAfter[key].Should().Be(etagBefore,
                $"fixture {key.ResourceType}/{key.Id} must be byte-equivalent — additive evolution must not migrate existing documents");
        }

        // Cleanup the synthetic resource we created so we don't pollute the
        // shared fixture for subsequent tests in this collection.
        await DeleteSyntheticAsync(syntheticId);
    }

    private static SyntheticForTestResource BuildSyntheticResource(ResourceId id) =>
        new()
        {
            Id = id,
            ResourceType = SyntheticDiscriminator,
            Name = new ResourceName("synthetic-evolution-probe"),
            DisplayName = "Synthetic additive-evolution probe",
            NamespacePath = new NamespacePath("enterprise/test/synthetic"),
            Lifecycle = LifecycleState.Active,
            Version = new SemanticVersion(1, 0, 0),
            Audit = new AuditRecord(
                CreatedBy: TestActor,
                CreatedAt: DateTimeOffset.UtcNow,
                ModifiedBy: TestActor,
                ModifiedAt: DateTimeOffset.UtcNow),
            SyntheticPayload = "preserved-as-raw",
        };

    private async Task DeleteSyntheticAsync(ResourceId id)
    {
        try
        {
            await _fixture.ResourcesCosmosContainer.DeleteItemAsync<object>(
                id.ToString(),
                new PartitionKey(SyntheticDiscriminator));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone — fine.
        }
    }

    private async Task<Dictionary<FixtureKey, string>> SnapshotFixturesAsync()
    {
        var snapshot = new Dictionary<FixtureKey, string>();
        using var iterator = _fixture.ResourcesCosmosContainer.GetItemQueryIterator<EtagRow>(
            new QueryDefinition("SELECT c.id, c.resourceType, c._etag AS etag FROM c WHERE c.resourceType != @synthetic")
                .WithParameter("@synthetic", SyntheticDiscriminator));

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            foreach (var row in page)
            {
                if (row.Id is null || row.ResourceType is null || row.Etag is null)
                {
                    continue;
                }

                snapshot[new FixtureKey(row.Id, row.ResourceType)] = row.Etag;
            }
        }

        return snapshot;
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

    private sealed record EtagRow(string? Id, string? ResourceType, string? Etag);

    private readonly record struct FixtureKey(string Id, string ResourceType);
}

// Synthetic resource type used only by AdditiveEvolutionGuardTests. Lives in
// the test assembly so it is NEVER registered in the production composition
// root — the test registers it on the fly via ResourceTypeRegistry.Register
// and unregisters before exiting.
public sealed record SyntheticForTestResource : Resource
{
    public required string SyntheticPayload { get; init; }
}
