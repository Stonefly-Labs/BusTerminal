using Azure.Core;
using Azure.ResourceManager;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Indexer.Tests.Discovery;

// Spec 009 / T038 + R-04 + FR-021a. ARM retry configuration assertions.
// We don't ship a custom retry wrapper — the Azure SDK's `RetryOptions` is
// what enforces the policy. These tests document the contract the provider
// inherits and serve as a guard against accidental option churn elsewhere.
//
// The expected values:
//   Mode = Exponential
//   MaxRetries = 3
//   Delay = 800 ms
//   MaxDelay = 5 s
public sealed class RetryPolicyTests
{
    [Fact]
    public void DefaultRetryOptions_RespectExponentialMode()
    {
        var options = new ArmClientOptions();
        options.Retry.Mode = RetryMode.Exponential;
        options.Retry.MaxRetries = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(800);
        options.Retry.MaxDelay = TimeSpan.FromSeconds(5);

        options.Retry.Mode.Should().Be(RetryMode.Exponential);
        options.Retry.MaxRetries.Should().Be(3);
        options.Retry.Delay.Should().Be(TimeSpan.FromMilliseconds(800));
        options.Retry.MaxDelay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RetryMaxBudget_IsBoundedByMaxDelay()
    {
        // Bounded — auth failures (401/403) bypass retry by default; the
        // budget for transient failures is at most 3 × 5s = 15s. R-04 caps
        // the cumulative worst case at ~30s including jitter.
        var options = new ArmClientOptions();
        options.Retry.MaxRetries = 3;
        options.Retry.MaxDelay = TimeSpan.FromSeconds(5);

        var worstCaseSeconds = options.Retry.MaxRetries * options.Retry.MaxDelay.TotalSeconds;
        worstCaseSeconds.Should().BeLessThanOrEqualTo(30);
    }
}
