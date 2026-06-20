using System.Collections.Generic;
using BusTerminal.Indexer.Discovery.Classification;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Indexer.Tests.Discovery.Classification;

// Spec 009 / T035 + R-08. AzureSourcedHash is deterministic and
// order-independent. The hash is what the API-side comparison uses to detect
// changes — drift here would translate to false-positive "updated" writes
// (or worse, false-negative "unchanged" → curated metadata stale).
public sealed class AzureSourcedHashTests
{
    [Fact]
    public void Compute_StartsWithSha256Prefix()
    {
        var source = new Dictionary<string, object?>
        {
            ["status"] = "Active",
            ["lockDuration"] = "PT1M",
        };

        var hash = AzureSourcedHash.Compute(source);

        hash.Should().StartWith("sha256:");
    }

    [Fact]
    public void Compute_IsOrderIndependent()
    {
        var a = new Dictionary<string, object?>
        {
            ["alpha"] = 1,
            ["beta"] = 2,
            ["gamma"] = 3,
        };
        var b = new Dictionary<string, object?>
        {
            ["gamma"] = 3,
            ["alpha"] = 1,
            ["beta"] = 2,
        };

        AzureSourcedHash.Compute(a).Should().Be(AzureSourcedHash.Compute(b));
    }

    [Fact]
    public void Compute_DifferentValues_ProduceDifferentHashes()
    {
        var a = new Dictionary<string, object?> { ["status"] = "Active" };
        var b = new Dictionary<string, object?> { ["status"] = "Disabled" };

        AzureSourcedHash.Compute(a).Should().NotBe(AzureSourcedHash.Compute(b));
    }

    [Fact]
    public void Compute_HandlesNestedObjects()
    {
        var a = new Dictionary<string, object?>
        {
            ["forwarding"] = new Dictionary<string, object?>
            {
                ["forwardTo"] = "queue-x",
                ["forwardDeadLetteredMessagesTo"] = null,
            },
        };
        var b = new Dictionary<string, object?>
        {
            ["forwarding"] = new Dictionary<string, object?>
            {
                ["forwardDeadLetteredMessagesTo"] = null,
                ["forwardTo"] = "queue-x",
            },
        };

        AzureSourcedHash.Compute(a).Should().Be(AzureSourcedHash.Compute(b));
    }

    [Fact]
    public void Compute_HandlesArrays()
    {
        var a = new Dictionary<string, object?>
        {
            ["filters"] = new object?[] { "sql:1", "sql:2", "sql:3" },
        };

        // Identical input → identical hash.
        AzureSourcedHash.Compute(a).Should().Be(AzureSourcedHash.Compute(a));

        var b = new Dictionary<string, object?>
        {
            ["filters"] = new object?[] { "sql:1", "sql:2" },
        };
        AzureSourcedHash.Compute(a).Should().NotBe(AzureSourcedHash.Compute(b));
    }

    [Fact]
    public void Compute_NullValues_AreCanonicalized()
    {
        var a = new Dictionary<string, object?> { ["forwardTo"] = null };
        var b = new Dictionary<string, object?> { ["forwardTo"] = null };

        AzureSourcedHash.Compute(a).Should().Be(AzureSourcedHash.Compute(b));
    }
}
