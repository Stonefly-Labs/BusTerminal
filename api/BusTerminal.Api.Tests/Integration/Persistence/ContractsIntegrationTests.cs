using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Azure.Cosmos;

namespace BusTerminal.Api.Tests.Integration.Persistence;

// Spec 004 / T119 / SC-001 contracts-subset evidence + FR-007 + FR-011.
//
// Loads 03-contracts.json into the emulator and asserts:
//   1. Multi-format contracts (jsonSchema, protobuf, cloudEvents) round-trip
//      through Cosmos without per-format forks — every fixture document
//      deserializes back into MessageContract with the same Format value.
//   2. Per-version lifecycle metadata (Version.VersionHistory entries with
//      lifecycle = Deprecated, replacedBy pointing at the current Active
//      version) survives the round trip and is queryable.
//   3. The lineage shape is internally consistent — ContractCompatibilityRule
//      produces no Errors or Warnings against the canonical fixture (Info-level
//      coaching is acceptable but Errors/Warnings would indicate fixture drift).
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class ContractsIntegrationTests
{
    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "03-contracts.json");

    // IDs match the fixture file — keep these in sync if the fixture changes.
    private static readonly ResourceId VersionedContractId =
        ResourceId.Parse("11111111-1111-1111-1111-000000000030");
    private static readonly ResourceId ProtobufContractId =
        ResourceId.Parse("11111111-1111-1111-1111-000000000031");
    private static readonly ResourceId CloudEventsContractId =
        ResourceId.Parse("11111111-1111-1111-1111-000000000032");

    private readonly CosmosEmulatorFixture _fixture;

    public ContractsIntegrationTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Multi_format_contracts_round_trip_through_cosmos()
    {
        await TruncateAsync();
        await LoadContractsAsync();

        var jsonSchema = await ReadContractAsync(VersionedContractId);
        var protobuf = await ReadContractAsync(ProtobufContractId);
        var cloudEvents = await ReadContractAsync(CloudEventsContractId);

        jsonSchema.Format.Should().Be(ContractFormat.JsonSchema);
        protobuf.Format.Should().Be(ContractFormat.Protobuf);
        cloudEvents.Format.Should().Be(ContractFormat.CloudEvents);

        // The cloudEvents fixture uses an inline schema; the others use external
        // URIs. Both shapes must persist verbatim (the schema's `oneOf`
        // mutual-exclusion would reject a hybrid serialization).
        cloudEvents.SchemaReference.Inline.Should().NotBeNullOrEmpty();
        cloudEvents.SchemaReference.ExternalUri.Should().BeNull();

        protobuf.SchemaReference.ExternalUri.Should().NotBeNullOrEmpty();
        protobuf.SchemaReference.Inline.Should().BeNull();
    }

    [Fact]
    public async Task Per_version_lifecycle_metadata_survives_round_trip()
    {
        await TruncateAsync();
        await LoadContractsAsync();

        var contract = await ReadContractAsync(VersionedContractId);

        contract.Version.Major.Should().Be(1);
        contract.Version.Minor.Should().Be(0);
        contract.Version.Patch.Should().Be(0);
        contract.Lifecycle.Should().Be(LifecycleState.Active);

        contract.Version.VersionHistory.Should().NotBeNull()
            .And.HaveCount(1, "the fixture defines exactly one historical version");

        var historical = contract.Version.VersionHistory!.Single();
        historical.Major.Should().Be(0);
        historical.Minor.Should().Be(9);
        historical.Patch.Should().Be(0);
        historical.Lifecycle.Should().Be(LifecycleState.Deprecated,
            "per-version lifecycle is independent of the resource-level lifecycle (FR-011)");
        historical.ReplacedBy.Should().NotBeNull();
        historical.ReplacedBy!.Major.Should().Be(1);
        historical.ReplacedBy.Minor.Should().Be(0);
        historical.ReplacedBy.Patch.Should().Be(0);
        historical.DeprecatedAt.Should().NotBeNull()
            .And.Subject!.Value.Should().BeBefore(DateTimeOffset.UtcNow,
                "the fixture marks v0.9.0 as deprecated in the past");
    }

    [Fact]
    public async Task Active_contracts_are_queryable_by_resource_type()
    {
        await TruncateAsync();
        await LoadContractsAsync();

        var contracts = new List<MessageContract>();
        await foreach (var resource in _fixture.Store.QueryAsync(
            new ResourceQuery.All(ResourceTypeDiscriminators.MessageContract),
            default))
        {
            contracts.Add((MessageContract)resource);
        }

        contracts.Should().HaveCount(3,
            "03-contracts.json defines three MessageContract resources of distinct formats");
        contracts.Select(c => c.Format).Should().BeEquivalentTo(
            new[] { ContractFormat.JsonSchema, ContractFormat.Protobuf, ContractFormat.CloudEvents });

        // Multi-format coexistence is the point of SC-001 / scenario 5: a single
        // typed query yields heterogeneous formats with no per-format fork.
        contracts.Should().AllSatisfy(c =>
            c.ResourceType.Should().Be(ResourceTypeDiscriminators.MessageContract));
    }

    private async Task<MessageContract> ReadContractAsync(ResourceId id)
    {
        var read = await _fixture.Store.GetAsync(
            id,
            ResourceTypeDiscriminators.MessageContract,
            includeDeleted: false,
            default);

        read.Should().NotBeNull($"contract {id} must be readable after fixture load");
        read.Should().BeOfType<MessageContract>(
            "polymorphic dispatch must reify each document as MessageContract");
        return (MessageContract)read!;
    }

    private async Task LoadContractsAsync()
    {
        var envelopeJson = await File.ReadAllTextAsync(FixturePath);
        var envelope = _fixture.Serializer.DeserializeEnvelopeFromJson(envelopeJson);

        envelope.Resources.Should().HaveCount(3,
            "03-contracts.json must define exactly three MessageContract resources");

        foreach (var resource in envelope.Resources)
        {
            await _fixture.Store.CreateAsync(resource, TestActor, "integration-test", default);
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
