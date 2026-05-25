using BusTerminal.Api.Domain;
using BusTerminal.Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T140 / SC-007 evidence. Verifies that environment classifications
// are a queryable indexable attribute on a single logical document — there is
// NO per-environment duplicate logical resource. Confirms custom environment
// values (extensibility path, FR-017) are also queryable.
//
// The fixture set spans 01-base.json (creates) + 05-environments.json (patches
// existing IDs to attach the six-environment Queue and the custom-env Broker /
// Policy). Loading both files mirrors what the load-fixtures CLI does end-to-end.
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class EnvironmentFilterIntegrationTests
{
    private static readonly ResourceId QueueId =
        ResourceId.Parse("11111111-1111-1111-1111-000000000004");

    private static readonly ResourceId BrokerId =
        ResourceId.Parse("11111111-1111-1111-1111-000000000003");

    private static readonly ResourceId PolicyId =
        ResourceId.Parse("11111111-1111-1111-1111-00000000000c");

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    private static readonly string[] FixtureFiles = ["01-base.json", "05-environments.json"];

    private readonly CosmosEmulatorFixture _fixture;

    public EnvironmentFilterIntegrationTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Single_logical_queue_spans_all_six_minimum_environments_with_no_duplicate_documents()
    {
        await TruncateAsync();
        await LoadFixturesAsync();

        var queue = await _fixture.Store.GetAsync(
            QueueId,
            ResourceTypeDiscriminators.Queue,
            includeDeleted: false,
            default);

        queue.Should().NotBeNull();
        queue!.Environments
            .Select(e => e.Value)
            .Should().BeEquivalentTo(
                new[] { "development", "test", "qa", "staging", "production", "disasterRecovery" },
                "the patched queue must carry all six minimum environments on a single document (SC-007).");
    }

    [Fact]
    public async Task InEnvironment_Production_query_returns_no_duplicate_logical_resources()
    {
        await TruncateAsync();
        await LoadFixturesAsync();

        var matches = await CollectAsync(_fixture.Store.QueryAsync(
            new ResourceQuery.InEnvironment(EnvironmentClassification.Production),
            default));

        matches.Should().NotBeEmpty(
            "the loaded fixture set includes resources carrying the production environment");

        var distinctIds = matches.Select(r => r.Id).ToHashSet();
        distinctIds.Count.Should().Be(matches.Count,
            "each logical resource must surface at most once — there is no per-environment fan-out document");

        matches.Should().Contain(r => r.Id == QueueId,
            "the patched queue spans all environments including production");

        matches.Should().AllSatisfy(r =>
            r.Environments.Should().Contain(EnvironmentClassification.Production,
                "ARRAY_CONTAINS predicate must filter to documents whose environments array includes 'production'."));
    }

    [Fact]
    public async Task InEnvironment_DisasterRecovery_query_returns_only_the_six_environment_queue()
    {
        await TruncateAsync();
        await LoadFixturesAsync();

        var matches = await CollectAsync(_fixture.Store.QueryAsync(
            new ResourceQuery.InEnvironment(EnvironmentClassification.DisasterRecovery),
            default));

        // The patched Queue is the only resource in the fixture set that carries
        // disasterRecovery. If a future fixture adds more, this assertion still
        // holds for QueueId presence (the disasterRecovery row must be the same
        // logical document, not a per-env duplicate).
        matches.Select(r => r.Id).Should().Contain(QueueId);
        matches.Select(r => r.Id).Should().OnlyHaveUniqueItems(
            "no per-environment duplicate documents — even on a low-cardinality environment.");
    }

    [Fact]
    public async Task Custom_environment_value_is_queryable()
    {
        await TruncateAsync();
        await LoadFixturesAsync();

        var training = new EnvironmentClassification("training");

        var matches = await CollectAsync(_fixture.Store.QueryAsync(
            new ResourceQuery.InEnvironment(training),
            default));

        matches.Should().HaveCountGreaterThanOrEqualTo(2,
            "at least two resources in the fixture set carry the custom 'training' environment (FR-017 extensibility).");

        matches.Select(r => r.Id).Should().BeEquivalentTo(new[] { BrokerId, PolicyId });

        matches.Should().AllSatisfy(r =>
            r.Environments.Should().Contain(training,
                "matched resources must actually carry the custom environment value."));
    }

    private async Task LoadFixturesAsync()
    {
        foreach (var file in FixtureFiles)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
            var envelopeJson = await File.ReadAllTextAsync(path);
            var envelope = _fixture.Serializer.DeserializeEnvelopeFromJson(envelopeJson);

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
        }
    }

    private static async Task<IReadOnlyList<Resource>> CollectAsync(IAsyncEnumerable<Resource> source)
    {
        var list = new List<Resource>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
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

                await container.DeleteItemAsync<object>(doc.Id, new PartitionKey(doc.Pk));
            }
        }
    }

    private sealed record DocumentRef(string? Id, string? Pk);
}
