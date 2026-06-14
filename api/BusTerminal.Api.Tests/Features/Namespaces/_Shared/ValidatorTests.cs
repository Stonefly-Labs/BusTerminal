using System.Text.Json;
using BusTerminal.Api.Features.Namespaces.Lifecycle;
using BusTerminal.Api.Features.Namespaces.Metadata;
using BusTerminal.Api.Features.Namespaces.Onboarding;
using BusTerminal.Api.Features.Namespaces.Ownership;
using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Registry.Shared;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Namespaces.Shared;

// Spec 008 / T042. Pass/fail coverage matrix for each namespace validator.
// One happy path + one fail per rule; OnboardingValidator gets dedicated
// coverage for its three async rules (ARM verification, duplicate ARM id,
// validation-run freshness).
public sealed class ValidatorTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid SubscriptionId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid NamespaceId = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private static readonly Guid ValidationRunId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private const string ValidArmId = "/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg-payments-prod/providers/Microsoft.ServiceBus/namespaces/orders-prod-eus2";

    // === UpdateMetadataValidator ===

    [Fact]
    public async Task UpdateMetadataValidator_HappyPath_Passes()
    {
        var validator = new UpdateMetadataValidator();
        var request = new UpdateMetadataRequest(
            Id: NamespaceId,
            DisplayName: "Orders Prod",
            Description: "Authoritative messaging",
            BusinessUnit: "Payments",
            ProductOrApplication: "Orders",
            CostCenter: "CC-42",
            Notes: null,
            Tags: null);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue(BuildErrorSummary(result));
    }

    [Fact]
    public async Task UpdateMetadataValidator_BlankDisplayName_Fails()
    {
        var validator = new UpdateMetadataValidator();
        var request = new UpdateMetadataRequest(NamespaceId, "", null, null, null, null, null, null);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("azureResourceId")]
    [InlineData("subscriptionId")]
    [InlineData("resourceGroup")]
    [InlineData("tenantId")]
    [InlineData("region")]
    [InlineData("namespaceName")]
    public async Task UpdateMetadataValidator_ProhibitedAzureField_Fails(string key)
    {
        var validator = new UpdateMetadataValidator();
        var bodyJson = $$"""{ "displayName": "X", "{{key}}": "v" }""";
        var rawBody = JsonDocument.Parse(bodyJson).RootElement;
        var request = new UpdateMetadataRequest(NamespaceId, "X", null, null, null, null, null, null, RawBody: rawBody);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    // === UpdateOwnershipValidator ===

    [Fact]
    public async Task UpdateOwnershipValidator_HappyPath_Passes()
    {
        var validator = new UpdateOwnershipValidator();
        var request = new UpdateOwnershipRequest(NamespaceId, NewOwnershipBlock());

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue(BuildErrorSummary(result));
    }

    [Fact]
    public async Task UpdateOwnershipValidator_MissingPrimaryOwner_Fails()
    {
        var validator = new UpdateOwnershipValidator();
        var request = new UpdateOwnershipRequest(NamespaceId, Ownership: null!);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateOwnershipValidator_DuplicateSecondaryOwner_Fails()
    {
        var validator = new UpdateOwnershipValidator();
        var duplicateId = Guid.NewGuid();
        var block = new OwnershipBlock(
            PrimaryOwner: NewAssignment(OwnershipRole.PrimaryOwner),
            SecondaryOwners:
            [
                NewAssignment(OwnershipRole.SecondaryOwner, duplicateId),
                NewAssignment(OwnershipRole.SecondaryOwner, duplicateId),
            ]);
        var request = new UpdateOwnershipRequest(NamespaceId, block);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    // === LifecycleTransitionValidator ===

    [Theory]
    [InlineData(LifecycleStatus.Active, LifecycleAction.Disable, true)]
    [InlineData(LifecycleStatus.Disabled, LifecycleAction.Enable, true)]
    [InlineData(LifecycleStatus.Active, LifecycleAction.Archive, true)]
    [InlineData(LifecycleStatus.Disabled, LifecycleAction.Archive, true)]
    [InlineData(LifecycleStatus.Archived, LifecycleAction.Restore, true)]
    [InlineData(LifecycleStatus.Active, LifecycleAction.Enable, false)]
    [InlineData(LifecycleStatus.Active, LifecycleAction.Restore, false)]
    [InlineData(LifecycleStatus.Archived, LifecycleAction.Disable, false)]
    [InlineData(LifecycleStatus.Archived, LifecycleAction.Archive, false)]
    public async Task LifecycleTransitionValidator_TransitionTable(
        LifecycleStatus current, LifecycleAction action, bool expectedValid)
    {
        var validator = new LifecycleTransitionValidator();
        var reason = action is LifecycleAction.Enable ? null : "operator note";
        var input = new LifecycleTransitionValidationInput(
            new LifecycleTransitionRequest(NamespaceId, action, reason),
            current);

        var result = await validator.ValidateAsync(input);

        result.IsValid.Should().Be(expectedValid, BuildErrorSummary(result));
    }

    [Theory]
    [InlineData(LifecycleAction.Disable)]
    [InlineData(LifecycleAction.Archive)]
    [InlineData(LifecycleAction.Restore)]
    public async Task LifecycleTransitionValidator_ReasonRequired_Fails(LifecycleAction action)
    {
        var validator = new LifecycleTransitionValidator();
        var current = action is LifecycleAction.Restore ? LifecycleStatus.Archived : LifecycleStatus.Active;
        var input = new LifecycleTransitionValidationInput(
            new LifecycleTransitionRequest(NamespaceId, action, Reason: null),
            current);

        var result = await validator.ValidateAsync(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Reason"));
    }

    // === OnboardingValidator ===

    [Fact]
    public async Task OnboardingValidator_HappyPath_Passes()
    {
        var validator = NewOnboardingValidator(
            new StubArmTenantResolver((_, _) => Task.FromResult(new TenantResolution(TenantId, TenantResolutionOutcome.Resolved))),
            entityStore: new StubEntityStore(findByArmId: null),
            validationRunStore: new StubValidationRunStore(NewHealthyRun(executedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1))));

        var request = NewOnboardingRequest();

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue(BuildErrorSummary(result));
    }

    [Fact]
    public async Task OnboardingValidator_DuplicateArmId_Fails()
    {
        var existing = new RegistryNamespace(
            id: Guid.NewGuid(), name: "orders-prod-eus2", environment: "prod",
            status: RegistryEntityStatus.Active,
            createdAtUtc: DateTimeOffset.UtcNow, updatedAtUtc: DateTimeOffset.UtcNow,
            source: RegistrySource.Onboarded,
            azureResourceId: ValidArmId);

        var validator = NewOnboardingValidator(
            new StubArmTenantResolver((_, _) => Task.FromResult(new TenantResolution(TenantId, TenantResolutionOutcome.Resolved))),
            entityStore: new StubEntityStore(findByArmId: existing),
            validationRunStore: new StubValidationRunStore(NewHealthyRun(executedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1))));

        var result = await validator.ValidateAsync(NewOnboardingRequest());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("already onboarded"));
    }

    [Fact]
    public async Task OnboardingValidator_CrossTenantArmId_Fails()
    {
        var validator = NewOnboardingValidator(
            new StubArmTenantResolver((_, _) => Task.FromResult(new TenantResolution(Guid.NewGuid(), TenantResolutionOutcome.Resolved))),
            entityStore: new StubEntityStore(findByArmId: null),
            validationRunStore: new StubValidationRunStore(NewHealthyRun(executedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1))));

        var result = await validator.ValidateAsync(NewOnboardingRequest());

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task OnboardingValidator_StaleValidationRun_Fails()
    {
        var validator = NewOnboardingValidator(
            new StubArmTenantResolver((_, _) => Task.FromResult(new TenantResolution(TenantId, TenantResolutionOutcome.Resolved))),
            entityStore: new StubEntityStore(findByArmId: null),
            validationRunStore: new StubValidationRunStore(NewHealthyRun(executedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-45))));

        var result = await validator.ValidateAsync(NewOnboardingRequest());

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task OnboardingValidator_UnhealthyValidationRun_Fails()
    {
        var run = NewHealthyRun(DateTimeOffset.UtcNow.AddMinutes(-1)) with { AggregateStatus = ValidationStatus.Unhealthy };
        var validator = NewOnboardingValidator(
            new StubArmTenantResolver((_, _) => Task.FromResult(new TenantResolution(TenantId, TenantResolutionOutcome.Resolved))),
            entityStore: new StubEntityStore(findByArmId: null),
            validationRunStore: new StubValidationRunStore(run));

        var result = await validator.ValidateAsync(NewOnboardingRequest());

        result.IsValid.Should().BeFalse("FR-023a hard-blocks register on Unhealthy aggregate");
    }

    [Fact]
    public async Task OnboardingValidator_BlankDisplayName_Fails()
    {
        var validator = NewOnboardingValidator(
            new StubArmTenantResolver((_, _) => Task.FromResult(new TenantResolution(TenantId, TenantResolutionOutcome.Resolved))),
            entityStore: new StubEntityStore(findByArmId: null),
            validationRunStore: new StubValidationRunStore(NewHealthyRun(executedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1))));

        var request = NewOnboardingRequest() with { DisplayName = "" };

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    // === Helpers / Stubs ===

    private static OnboardingRequest NewOnboardingRequest() => new(
        Id: NamespaceId,
        AzureResourceId: ValidArmId,
        DisplayName: "Orders Prod",
        Environment: "prod",
        Description: "Authoritative messaging",
        BusinessUnit: "Payments",
        ProductOrApplication: "Orders",
        CostCenter: "CC-42",
        Notes: null,
        Tags: null,
        Ownership: NewOwnershipBlock(),
        ValidationRunId: ValidationRunId);

    private static OwnershipBlock NewOwnershipBlock() => new(
        PrimaryOwner: NewAssignment(OwnershipRole.PrimaryOwner));

    private static OwnershipAssignment NewAssignment(OwnershipRole role, Guid? objectId = null) => new(
        Role: role,
        PrincipalType: PrincipalType.User,
        ObjectId: objectId ?? Guid.NewGuid(),
        DisplayNameSnapshot: "Jane Doe",
        AssignedAtUtc: DateTimeOffset.UtcNow,
        AssignedBy: Guid.NewGuid());

    private static ValidationRun NewHealthyRun(DateTimeOffset executedAtUtc) => new(
        Id: ValidationRunId,
        NamespaceId: NamespaceId,
        ExecutedAtUtc: executedAtUtc,
        ExecutedBy: Guid.NewGuid(),
        ExecutedByDisplayNameSnapshot: "Test Operator",
        AzureResourceIdAtRun: ValidArmId,
        AggregateStatus: ValidationStatus.Healthy,
        CheckResults:
        [
            new(ValidationCheckName.Existence, ValidationCheckOutcome.Pass, "OK", ValidationFailureCategory.Ok, 10),
            new(ValidationCheckName.Accessibility, ValidationCheckOutcome.Pass, "OK", ValidationFailureCategory.Ok, 10),
            new(ValidationCheckName.RequiredPermissions, ValidationCheckOutcome.Pass, "OK", ValidationFailureCategory.Ok, 10),
            new(ValidationCheckName.IdentityAuthorization, ValidationCheckOutcome.Pass, "OK", ValidationFailureCategory.Ok, 10),
            new(ValidationCheckName.ApiReachability, ValidationCheckOutcome.Pass, "OK", ValidationFailureCategory.Ok, 10),
        ],
        DriftDetected: false,
        DriftFields: Array.Empty<DriftField>(),
        TotalDurationMs: 50);

    private static OnboardingValidator NewOnboardingValidator(
        IArmSubscriptionTenantResolver resolver,
        IRegistryEntityStore entityStore,
        INamespaceValidationRunStore validationRunStore)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:TenantId"] = TenantId.ToString("D"),
            })
            .Build();
        var parser = new NamespaceArmIdParser(resolver, configuration);
        return new OnboardingValidator(parser, entityStore, validationRunStore, TimeProvider.System);
    }

    private static string BuildErrorSummary(ValidationResult result)
        => result.IsValid ? "valid" : string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));

    private sealed class StubArmTenantResolver : IArmSubscriptionTenantResolver
    {
        private readonly Func<Guid, CancellationToken, Task<TenantResolution>> _fn;
        public StubArmTenantResolver(Func<Guid, CancellationToken, Task<TenantResolution>> fn) => _fn = fn;
        public Task<TenantResolution> ResolveTenantIdAsync(Guid subscriptionId, CancellationToken ct) => _fn(subscriptionId, ct);
    }

    private sealed class StubEntityStore : IRegistryEntityStore
    {
        private readonly RegistryEntity? _findByArmId;
        public StubEntityStore(RegistryEntity? findByArmId) => _findByArmId = findByArmId;

        public Task<RegistryEntity?> FindByAzureResourceIdAsync(string armId, CancellationToken ct)
            => Task.FromResult(_findByArmId);

        public Task<RegistryEntity?> GetAsync(Guid id, string env, CancellationToken ct) => Task.FromResult<RegistryEntity?>(null);
        public Task<RegistryEntityPage> ListAsync(RegistryEntityListQuery q, CancellationToken ct) => Task.FromResult(new RegistryEntityPage(Array.Empty<RegistryEntity>(), null));
        public Task<RegistryEntity> CreateAsync(RegistryEntity e, CancellationToken ct) => Task.FromResult(e);
        public Task<RegistryEntity> UpdateAsync(RegistryEntity e, string ifMatch, CancellationToken ct) => Task.FromResult(e);
        public Task DeleteAsync(Guid id, string env, string ifMatch, CancellationToken ct) => Task.CompletedTask;
        public Task<ChildCount> CountChildrenAsync(Guid parentId, string env, CancellationToken ct)
            => Task.FromResult(new ChildCount(0, new Dictionary<RegistryEntityType, int>()));
        public Task<RegistryEntity?> FindByParentAndNameAsync(Guid? parentId, RegistryEntityType type, string name, string env, CancellationToken ct) => Task.FromResult<RegistryEntity?>(null);
        public Task<RegistryEntity?> FindParentAsync(Guid parentId, RegistryEntityType type, string env, CancellationToken ct) => Task.FromResult<RegistryEntity?>(null);
        public Task<IReadOnlyList<string>> ListDistinctEnvironmentsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<RegistryEntity?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<RegistryEntity?>(null);
    }

    private sealed class StubValidationRunStore : INamespaceValidationRunStore
    {
        private readonly ValidationRun _run;
        public StubValidationRunStore(ValidationRun run) => _run = run;
        public Task AppendAsync(ValidationRun run, CancellationToken ct) => Task.CompletedTask;
        public Task<ValidationRunPage> ListForNamespaceAsync(Guid ns, int limit, string? token, CancellationToken ct)
            => Task.FromResult(new ValidationRunPage(new[] { _run }, null));
        public Task<ValidationRun?> GetAsync(Guid ns, Guid runId, CancellationToken ct)
            => Task.FromResult<ValidationRun?>(runId == _run.Id ? _run : null);
    }
}
