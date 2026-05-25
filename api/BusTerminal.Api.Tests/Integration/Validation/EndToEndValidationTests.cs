using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Resources;
using BusTerminal.Api.Domain.Validation;
using BusTerminal.Api.Infrastructure.Persistence;
using BusTerminal.Api.Tests.Integration.Persistence;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace BusTerminal.Api.Tests.Integration.Validation;

// Spec 004 / T130 / FR-013 / SC-008 evidence.
//
// Drives the validation pipeline end-to-end against the live Cosmos emulator
// to verify the contract that Error severity blocks writes while Warning and
// Info severities persist with the resulting ValidationResult stamped onto the
// resource. The test composes the pipeline the same way the CLI verbs (and
// future API handlers) do: GetAsync to read the prior state, ValidateAsync to
// generate findings, then conditionally Create/Update.
[Collection("CosmosEmulator")]
[Trait("Category", "Integration")]
public sealed class EndToEndValidationTests
{
    private readonly CosmosEmulatorFixture _fixture;

    public EndToEndValidationTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly PrincipalReference TestActor =
        new SystemPrincipalReference("integration-test");

    private static readonly ResourceId SampleTeamId =
        ResourceId.Parse("11111111-1111-1111-1111-000000000001");

    [Fact]
    public async Task Error_finding_blocks_the_write()
    {
        await TruncateAsync();
        await SeedTeamAsync();

        var topic = await CreateValidatedAsync(BuildTopic("error-block-topic", LifecycleState.Active));
        topic.IsWritten.Should().BeTrue("baseline topic must persist before we attempt an illegal transition");

        // Active -> Archived is rejected by LifecycleTransitionRule with Error
        // severity per contracts/lifecycle-transitions.md.
        var illegalUpdate = ((Topic)topic.Stored!) with { Lifecycle = LifecycleState.Archived };
        var attempt = await UpdateValidatedAsync(illegalUpdate, previousLifecycle: LifecycleState.Active);

        attempt.IsWritten.Should().BeFalse("Error findings must block the write");
        attempt.Validation.HasErrors.Should().BeTrue();
        attempt.Validation.Findings.Should().Contain(f =>
            f.Severity == ValidationSeverity.Error &&
            f.RuleId.StartsWith("lifecycle.transition", StringComparison.Ordinal));

        var stored = await _fixture.Store.GetAsync(topic.Stored!.Id, topic.Stored.ResourceType, includeDeleted: false, default);
        stored.Should().NotBeNull();
        stored!.Lifecycle.Should().Be(LifecycleState.Active,
            "the prior state must survive because the write was blocked");
    }

    [Fact]
    public async Task Warning_finding_persists_with_the_resource()
    {
        await TruncateAsync();
        await SeedTeamAsync();

        // Set up a Topic + child Subscription, soft-delete the Subscription,
        // then create a second Topic that references the soft-deleted
        // Subscription. The dangling-soft-deleted reference fires
        // DanglingReferenceRule at Warning severity per the rule's contract for
        // soft-deleted referents.
        var parentTopic = await CreateValidatedAsync(BuildTopic("warning-parent-topic", LifecycleState.Active));
        parentTopic.IsWritten.Should().BeTrue();

        var subscription = await CreateValidatedAsync(BuildSubscription("warning-subscription", parentTopic.Stored!.Id));
        subscription.IsWritten.Should().BeTrue();

        var stored = subscription.Stored!;
        await _fixture.Store.SoftDeleteAsync(
            stored.Id,
            stored.ResourceType,
            stored.ConcurrencyToken,
            TestActor,
            "integration-test",
            default);

        var topic = BuildTopic("warning-topic", LifecycleState.Active) with
        {
            SubscriptionIds = [stored.Id],
        };

        var result = await CreateValidatedAsync(topic);

        result.IsWritten.Should().BeTrue("Warning findings do not block the write");
        result.Validation.HasErrors.Should().BeFalse();
        result.Validation.Findings.Should().Contain(f =>
            f.Severity == ValidationSeverity.Warning &&
            f.RuleId == "reference.dangling");

        var persisted = await _fixture.Store.GetAsync(topic.Id, topic.ResourceType, includeDeleted: false, default);
        persisted.Should().NotBeNull();
        persisted!.ValidationState.Should().NotBeNull("ValidationState is part of the persisted document");
        persisted.ValidationState!.Findings.Should().Contain(f =>
            f.Severity == ValidationSeverity.Warning,
            "Warning findings survive the round-trip");
    }

    [Fact]
    public async Task Info_finding_persists_with_the_resource()
    {
        await TruncateAsync();
        await SeedTeamAsync();

        // A MessageContract with a Deprecated history entry that has no
        // replacedBy reference but a non-deprecated successor (the resource's
        // own current Active version) fires ContractCompatibilityRule at Info.
        var contract = new MessageContract
        {
            Id = ResourceId.New(),
            ResourceType = ResourceTypeDiscriminators.MessageContract,
            Name = new ResourceName("info-contract"),
            DisplayName = "Info contract",
            NamespacePath = new NamespacePath("enterprise/test/contracts"),
            Lifecycle = LifecycleState.Active,
            Version = new SemanticVersion(
                Major: 1,
                Minor: 0,
                Patch: 0,
                Compatibility: CompatibilityIndicator.Backward,
                VersionHistory:
                [
                    new HistoricalVersionEntry(0, 9, 0, LifecycleState.Deprecated),
                ]),
            Audit = new AuditRecord(TestActor, DateTimeOffset.UtcNow, TestActor, DateTimeOffset.UtcNow),
            Ownership = new OwnershipRecord(SampleTeamId, OperationalTier.Tier2),
            Format = ContractFormat.JsonSchema,
            SchemaReference = SchemaReference.FromExternalUri("https://schemas.example.com/info-contract/v1.json"),
            Compatibility = CompatibilityIndicator.Backward,
        };

        var result = await CreateValidatedAsync(contract);

        result.IsWritten.Should().BeTrue("Info findings do not block the write");
        result.Validation.Findings.Should().Contain(f =>
            f.Severity == ValidationSeverity.Info &&
            f.RuleId == "contract.compatibility");

        var persisted = await _fixture.Store.GetAsync(contract.Id, contract.ResourceType, includeDeleted: false, default);
        persisted.Should().NotBeNull();
        persisted!.ValidationState.Should().NotBeNull();
        persisted.ValidationState!.Findings.Should().Contain(f =>
            f.Severity == ValidationSeverity.Info,
            "Info findings survive the round-trip");
    }

    // ----- pipeline helpers -----

    private sealed record WriteAttempt(bool IsWritten, ValidationResult Validation, Resource? Stored);

    private async Task<WriteAttempt> CreateValidatedAsync(Resource resource)
    {
        var engine = _fixture.Services.GetRequiredService<ValidationEngine>();
        var resolver = BuildResolver();

        var validation = await engine.ValidateAsync(
            resource,
            relationshipResolver: resolver,
            duplicateDetector: _ => false,
            previousLifecycle: null);

        if (validation.HasErrors)
        {
            return new WriteAttempt(false, validation, null);
        }

        var stamped = resource with { ValidationState = validation };
        var written = await _fixture.Store.CreateAsync(stamped, TestActor, "integration-test", default);
        return new WriteAttempt(true, validation, written);
    }

    private async Task<WriteAttempt> UpdateValidatedAsync(Resource resource, LifecycleState previousLifecycle)
    {
        var engine = _fixture.Services.GetRequiredService<ValidationEngine>();
        var resolver = BuildResolver();

        var validation = await engine.ValidateAsync(
            resource,
            relationshipResolver: resolver,
            duplicateDetector: _ => false,
            previousLifecycle: previousLifecycle);

        if (validation.HasErrors)
        {
            return new WriteAttempt(false, validation, null);
        }

        var stamped = resource with { ValidationState = validation };
        var written = await _fixture.Store.UpdateAsync(stamped, TestActor, "integration-test", default);
        return new WriteAttempt(true, validation, written);
    }

    private Func<ResourceId, Resource?> BuildResolver()
    {
        // Synchronous over the async store API for the validation context. The
        // emulator is local and the candidate set is small; blocking is fine for
        // these tests and mirrors the loaded-fixture caches used by ImportCommand.
        var registry = _fixture.Services.GetRequiredService<ResourceTypeRegistry>();
        return id =>
        {
            foreach (var discriminator in registry.KnownDiscriminators)
            {
                var resource = _fixture.Store.GetAsync(id, discriminator, includeDeleted: true, default).GetAwaiter().GetResult();
                if (resource is not null)
                {
                    return resource;
                }
            }

            return null;
        };
    }

    private async Task SeedTeamAsync()
    {
        var team = new Team
        {
            Id = SampleTeamId,
            ResourceType = ResourceTypeDiscriminators.Team,
            Name = new ResourceName("e2e-validation-team"),
            DisplayName = "E2E validation team",
            NamespacePath = new NamespacePath("enterprise/test"),
            Lifecycle = LifecycleState.Active,
            Version = new SemanticVersion(1, 0, 0),
            Audit = new AuditRecord(TestActor, DateTimeOffset.UtcNow, TestActor, DateTimeOffset.UtcNow),
            Slug = "e2e-validation-team",
            OperationalTier = OperationalTier.Tier1,
        };

        await _fixture.Store.CreateAsync(team, TestActor, "integration-test", default);
    }

    private static Topic BuildTopic(string name, LifecycleState lifecycle) => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Topic,
        Name = new ResourceName(name),
        DisplayName = $"E2E validation topic {name}",
        NamespacePath = new NamespacePath("enterprise/test/topics"),
        Lifecycle = lifecycle,
        Version = new SemanticVersion(1, 0, 0),
        Audit = new AuditRecord(TestActor, DateTimeOffset.UtcNow, TestActor, DateTimeOffset.UtcNow),
        Ownership = new OwnershipRecord(SampleTeamId, OperationalTier.Tier2),
        Ordering = OrderingPolicy.Unordered,
    };

    private static Subscription BuildSubscription(string name, ResourceId parentTopicId) => new()
    {
        Id = ResourceId.New(),
        ResourceType = ResourceTypeDiscriminators.Subscription,
        Name = new ResourceName(name),
        DisplayName = $"E2E validation subscription {name}",
        NamespacePath = new NamespacePath("enterprise/test/subs"),
        Lifecycle = LifecycleState.Active,
        Version = new SemanticVersion(1, 0, 0),
        Audit = new AuditRecord(TestActor, DateTimeOffset.UtcNow, TestActor, DateTimeOffset.UtcNow),
        Ownership = new OwnershipRecord(SampleTeamId, OperationalTier.Tier2),
        ParentTopicId = parentTopicId,
        DeliverySemantics = DeliverySemantics.AtLeastOnce,
    };

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
