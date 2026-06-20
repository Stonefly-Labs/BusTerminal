using BusTerminal.Indexer.Discovery;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Indexer.Tests.Discovery;

// Spec 009 / T053a. FailureMessageSanitizer keeps PII (ARM ids, namespace
// names, entity names) out of the persisted DiscoveryRun.failure.message.
public sealed class FailureMessageSanitizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_NullOrWhitespace_ReturnsFallback(string? input)
    {
        FailureMessageSanitizer.Sanitize(input).Should().Be(FailureMessageSanitizer.Fallback);
    }

    [Fact]
    public void Sanitize_RedactsArmResourcePaths()
    {
        var message = "Could not access /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/rg-payments/providers/Microsoft.ServiceBus/namespaces/payments-prod/queues/orders-inbox";

        var result = FailureMessageSanitizer.Sanitize(message);

        result.Should().Contain("(redacted-arm-id)");
        result.Should().NotContain("rg-payments");
        result.Should().NotContain("orders-inbox");
    }

    [Fact]
    public void Sanitize_RedactsBareGuids()
    {
        var message = "Subscription 11111111-2222-3333-4444-555555555555 is throttled.";

        var result = FailureMessageSanitizer.Sanitize(message);

        result.Should().Contain("(redacted-guid)");
        result.Should().NotContain("11111111-2222-3333-4444-555555555555");
    }

    [Fact]
    public void Sanitize_RedactsSingleQuotedNames()
    {
        var message = "Entity 'orders-inbox' could not be retrieved.";

        var result = FailureMessageSanitizer.Sanitize(message);

        result.Should().Contain("(redacted-name)");
    }

    [Fact]
    public void Sanitize_PassesThroughOperatorFriendlyCategoryWords()
    {
        var message = "Throttled by ARM after 3 retries (HTTP 429)";

        var result = FailureMessageSanitizer.Sanitize(message);

        result.Should().Contain("Throttled");
        result.Should().Contain("429");
    }

    [Fact]
    public void Sanitize_TruncatesVeryLongMessages()
    {
        var message = new string('x', FailureMessageSanitizer.MaxLength + 100);

        var result = FailureMessageSanitizer.Sanitize(message);

        result.Length.Should().BeLessThanOrEqualTo(FailureMessageSanitizer.MaxLength + "… (truncated)".Length);
        result.Should().EndWith("(truncated)");
    }
}
