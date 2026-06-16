using BusTerminal.Api.Features.Namespaces.Shared;
using BusTerminal.Api.Features.Namespaces.Validation.Checks;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Namespaces.Validation.Checks;

// Spec 008 / T055–T059. Per-check coverage. Each check is a thin adapter
// over IArmNamespaceProbe; the probe itself is exercised in detail by the
// ArmNamespaceProbe integration tests (existing). Here we verify the
// adapter responsibilities:
//   - Each check exposes the correct `Name`.
//   - Probe results round-trip into ValidationCheckResult with matching
//     outcome, reasonCategory, reason, and propagated correlationRequestId.
//   - Snapshot is propagated through CheckExecutionResult.
public sealed class ValidationChecksTests
{
    private static readonly NamespaceArmId ArmId = new(
        CanonicalArmId: "/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/ns",
        SubscriptionId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
        ResourceGroup: "rg",
        NamespaceName: "ns");

    public static IEnumerable<object[]> AllCheckShapes => new[]
    {
        new object[] { ValidationCheckName.Existence },
        new object[] { ValidationCheckName.Accessibility },
        new object[] { ValidationCheckName.RequiredPermissions },
        new object[] { ValidationCheckName.IdentityAuthorization },
        new object[] { ValidationCheckName.ApiReachability },
    };

    [Theory]
    [MemberData(nameof(AllCheckShapes))]
    public void Name_MatchesExpectedEnumValue(ValidationCheckName expected)
    {
        var check = CreateCheck(expected, new StubProbe(PassResult()));
        check.Name.Should().Be(expected);
    }

    [Fact]
    public async Task ExistenceCheck_HappyPath_ReturnsPassWithSnapshot()
    {
        var snapshot = new ArmResourceSnapshot("eastus2", "rg", ArmId.SubscriptionId, DateTimeOffset.UtcNow);
        var probe = new StubProbe(new ArmProbeResult(
            ValidationCheckOutcome.Pass,
            ValidationFailureCategory.Ok,
            "OK",
            CorrelationRequestId: "corr-1",
            Snapshot: snapshot));

        var check = new ExistenceCheck(probe);
        var result = await check.ExecuteAsync(ArmId, CancellationToken.None);

        result.Result.Outcome.Should().Be(ValidationCheckOutcome.Pass);
        result.Result.ReasonCategory.Should().Be(ValidationFailureCategory.Ok);
        result.Result.CorrelationRequestId.Should().Be("corr-1");
        result.Snapshot.Should().Be(snapshot);
    }

    [Fact]
    public async Task ExistenceCheck_NotFound_PropagatesFailureCategory()
    {
        var probe = new StubProbe(new ArmProbeResult(
            ValidationCheckOutcome.Fail,
            ValidationFailureCategory.NotFound,
            "ArmNamespaceNotFound"));

        var check = new ExistenceCheck(probe);
        var result = await check.ExecuteAsync(ArmId, CancellationToken.None);

        result.Result.Outcome.Should().Be(ValidationCheckOutcome.Fail);
        result.Result.ReasonCategory.Should().Be(ValidationFailureCategory.NotFound);
        result.Result.Reason.Should().Be("ArmNamespaceNotFound");
        result.Snapshot.Should().BeNull();
    }

    [Fact]
    public async Task AccessibilityCheck_Unauthorized_PropagatesUnauthorized()
    {
        var probe = new StubProbe(new ArmProbeResult(
            ValidationCheckOutcome.Fail,
            ValidationFailureCategory.Unauthorized,
            "ArmAccessDenied"));

        var check = new AccessibilityCheck(probe);
        var result = await check.ExecuteAsync(ArmId, CancellationToken.None);

        result.Result.ReasonCategory.Should().Be(ValidationFailureCategory.Unauthorized);
    }

    [Fact]
    public async Task RequiredPermissionsCheck_ReaderMissing_Fails()
    {
        var probe = new StubProbe(new ArmProbeResult(
            ValidationCheckOutcome.Fail,
            ValidationFailureCategory.Unauthorized,
            "ReaderRoleMissing"));

        var check = new RequiredPermissionsCheck(probe);
        var result = await check.ExecuteAsync(ArmId, CancellationToken.None);

        result.Result.Reason.Should().Be("ReaderRoleMissing");
    }

    [Fact]
    public async Task IdentityAuthorizationCheck_TokenExchangeFailure_PropagatesUnauthorized()
    {
        var probe = new StubProbe(new ArmProbeResult(
            ValidationCheckOutcome.Fail,
            ValidationFailureCategory.Unauthorized,
            "TokenExchangeFailed"));

        var check = new IdentityAuthorizationCheck(probe);
        var result = await check.ExecuteAsync(ArmId, CancellationToken.None);

        result.Result.Reason.Should().Be("TokenExchangeFailed");
    }

    [Fact]
    public async Task ApiReachabilityCheck_Network_Failure_Fails()
    {
        var probe = new StubProbe(new ArmProbeResult(
            ValidationCheckOutcome.Fail,
            ValidationFailureCategory.Unknown,
            "ServiceBusManagementUnreachable"));

        var check = new ApiReachabilityCheck(probe);
        var result = await check.ExecuteAsync(ArmId, CancellationToken.None);

        result.Result.Reason.Should().Be("ServiceBusManagementUnreachable");
    }

    [Theory]
    [MemberData(nameof(AllCheckShapes))]
    public async Task ExecuteAsync_PopulatesDurationMs(ValidationCheckName name)
    {
        var probe = new StubProbe(PassResult());
        var check = CreateCheck(name, probe);

        var result = await check.ExecuteAsync(ArmId, CancellationToken.None);

        result.Result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        result.Result.Name.Should().Be(name);
    }

    private static ArmProbeResult PassResult() => new(
        ValidationCheckOutcome.Pass,
        ValidationFailureCategory.Ok,
        "OK");

    private static INamespaceValidationCheck CreateCheck(ValidationCheckName name, IArmNamespaceProbe probe)
        => name switch
        {
            ValidationCheckName.Existence => new ExistenceCheck(probe),
            ValidationCheckName.Accessibility => new AccessibilityCheck(probe),
            ValidationCheckName.RequiredPermissions => new RequiredPermissionsCheck(probe),
            ValidationCheckName.IdentityAuthorization => new IdentityAuthorizationCheck(probe),
            ValidationCheckName.ApiReachability => new ApiReachabilityCheck(probe),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    private sealed class StubProbe : IArmNamespaceProbe
    {
        private readonly ArmProbeResult _result;

        public StubProbe(ArmProbeResult result) => _result = result;

        public Task<ArmProbeResult> ProbeExistenceAsync(NamespaceArmId armId, CancellationToken cancellationToken)
            => Task.FromResult(_result);
        public Task<ArmProbeResult> ProbeAccessibilityAsync(NamespaceArmId armId, CancellationToken cancellationToken)
            => Task.FromResult(_result);
        public Task<ArmProbeResult> ProbeRequiredPermissionsAsync(NamespaceArmId armId, CancellationToken cancellationToken)
            => Task.FromResult(_result);
        public Task<ArmProbeResult> ProbeIdentityAuthorizationAsync(NamespaceArmId armId, CancellationToken cancellationToken)
            => Task.FromResult(_result);
        public Task<ArmProbeResult> ProbeApiReachabilityAsync(NamespaceArmId armId, CancellationToken cancellationToken)
            => Task.FromResult(_result);
    }
}
