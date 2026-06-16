using BusTerminal.Api.Features.Namespaces.Shared;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BusTerminal.Api.Tests.Features.Namespaces.Shared;

// Spec 008 / T024. Coverage matrix per task description:
//   - malformed ARM id
//   - wrong resource type (EventHub, Relay)
//   - correct format with same-tenant subscription
//   - correct format with different-tenant subscription (CrossTenant)
//   - subscription cache hit (resolver called once across repeated parses)
//   - ARM throttling (Throttled category)
public sealed class NamespaceArmIdParserTests
{
    private static readonly Guid ConfiguredTenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid SubscriptionId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static readonly string ValidArmId =
        $"/subscriptions/{SubscriptionId:D}/resourceGroups/rg-payments-prod/providers/Microsoft.ServiceBus/namespaces/orders-prod-eus2";

    [Fact]
    public async Task ParseAndVerifyAsync_NullOrEmpty_ReturnsUnknown()
    {
        var parser = NewParser(static (_, _) => Task.FromResult(
            new TenantResolution(ConfiguredTenantId, TenantResolutionOutcome.Resolved)));

        var result = await parser.ParseAndVerifyAsync(string.Empty, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureCategory.Should().Be(ValidationFailureCategory.Unknown);
    }

    [Fact]
    public async Task ParseAndVerifyAsync_MalformedArmId_ReturnsUnknown()
    {
        var parser = NewParser(static (_, _) => Task.FromResult(
            new TenantResolution(ConfiguredTenantId, TenantResolutionOutcome.Resolved)));

        var result = await parser.ParseAndVerifyAsync(
            "/subscriptions/not-a-guid/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/n",
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureCategory.Should().Be(ValidationFailureCategory.Unknown);
        result.ArmId.Should().BeNull();
    }

    [Theory]
    [InlineData("EventHub")]
    [InlineData("Relay")]
    [InlineData("NotificationHubs")]
    public async Task ParseAndVerifyAsync_WrongResourceType_ReturnsNotFoundWithProviderHint(string provider)
    {
        var parser = NewParser(static (_, _) => Task.FromResult(
            new TenantResolution(ConfiguredTenantId, TenantResolutionOutcome.Resolved)));

        var armId = $"/subscriptions/{SubscriptionId:D}/resourceGroups/rg-x/providers/Microsoft.{provider}/namespaces/something";

        var result = await parser.ParseAndVerifyAsync(armId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureCategory.Should().Be(ValidationFailureCategory.NotFound);
        result.Reason.Should().Contain(provider);
    }

    [Fact]
    public async Task ParseAndVerifyAsync_SameTenantSubscription_ReturnsOk()
    {
        var parser = NewParser((_, _) => Task.FromResult(
            new TenantResolution(ConfiguredTenantId, TenantResolutionOutcome.Resolved)));

        var result = await parser.ParseAndVerifyAsync(ValidArmId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.FailureCategory.Should().Be(ValidationFailureCategory.Ok);
        result.ArmId.Should().NotBeNull();
        result.ArmId!.SubscriptionId.Should().Be(SubscriptionId);
        result.ArmId.ResourceGroup.Should().Be("rg-payments-prod");
        result.ArmId.NamespaceName.Should().Be("orders-prod-eus2");
        result.TenantId.Should().Be(ConfiguredTenantId);
    }

    [Fact]
    public async Task ParseAndVerifyAsync_DifferentTenantSubscription_ReturnsCrossTenant()
    {
        var foreignTenantId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var parser = NewParser((_, _) => Task.FromResult(
            new TenantResolution(foreignTenantId, TenantResolutionOutcome.Resolved)));

        var result = await parser.ParseAndVerifyAsync(ValidArmId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureCategory.Should().Be(ValidationFailureCategory.CrossTenant);
        result.ArmId.Should().NotBeNull("the parser surfaces the parsed identifier so the UI can render it in the error");
    }

    [Fact]
    public async Task ParseAndVerifyAsync_RepeatedParseForSameSubscription_HitsCache()
    {
        var calls = 0;
        var parser = NewParser((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new TenantResolution(ConfiguredTenantId, TenantResolutionOutcome.Resolved));
        });

        await parser.ParseAndVerifyAsync(ValidArmId, CancellationToken.None);
        await parser.ParseAndVerifyAsync(ValidArmId, CancellationToken.None);
        await parser.ParseAndVerifyAsync(ValidArmId, CancellationToken.None);

        calls.Should().Be(1, "subscription→tenant resolution is cached per parser instance per FR-006 / research §10");
    }

    [Fact]
    public async Task ParseAndVerifyAsync_ArmThrottling_ReturnsThrottled()
    {
        var parser = NewParser((_, _) => Task.FromResult(
            new TenantResolution(TenantId: null, TenantResolutionOutcome.Throttled, "429 from ARM")));

        var result = await parser.ParseAndVerifyAsync(ValidArmId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureCategory.Should().Be(ValidationFailureCategory.Throttled);
    }

    [Fact]
    public async Task ParseAndVerifyAsync_SubscriptionNotFound_ReturnsNotFound()
    {
        var parser = NewParser((_, _) => Task.FromResult(
            new TenantResolution(TenantId: null, TenantResolutionOutcome.SubscriptionNotFound)));

        var result = await parser.ParseAndVerifyAsync(ValidArmId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureCategory.Should().Be(ValidationFailureCategory.NotFound);
    }

    private static NamespaceArmIdParser NewParser(
        Func<Guid, CancellationToken, Task<TenantResolution>> resolveFn)
    {
        var resolver = new StubTenantResolver(resolveFn);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:TenantId"] = ConfiguredTenantId.ToString("D"),
            })
            .Build();
        return new NamespaceArmIdParser(resolver, configuration);
    }

    private sealed class StubTenantResolver : IArmSubscriptionTenantResolver
    {
        private readonly Func<Guid, CancellationToken, Task<TenantResolution>> _fn;
        public StubTenantResolver(Func<Guid, CancellationToken, Task<TenantResolution>> fn) => _fn = fn;
        public Task<TenantResolution> ResolveTenantIdAsync(Guid subscriptionId, CancellationToken ct)
            => _fn(subscriptionId, ct);
    }
}
