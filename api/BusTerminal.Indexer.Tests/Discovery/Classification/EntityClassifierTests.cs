using BusTerminal.Indexer.Discovery.Classification;
using FluentAssertions;
using Xunit;

namespace BusTerminal.Indexer.Tests.Discovery.Classification;

// Spec 009 / T036 + FR-013. EntityClassifier's three branches.
public sealed class EntityClassifierTests
{
    [Fact]
    public void Classify_NoPriorHash_IsNew()
    {
        EntityClassifier.Classify(priorHash: null, currentHash: "sha256:abc")
            .Should().Be(ClassificationOutcome.New);
    }

    [Fact]
    public void Classify_EmptyPriorHash_IsNew()
    {
        EntityClassifier.Classify(priorHash: "", currentHash: "sha256:abc")
            .Should().Be(ClassificationOutcome.New);
    }

    [Fact]
    public void Classify_MatchingHash_IsUnchanged()
    {
        EntityClassifier.Classify(priorHash: "sha256:abc", currentHash: "sha256:abc")
            .Should().Be(ClassificationOutcome.Unchanged);
    }

    [Fact]
    public void Classify_DifferentHash_IsUpdated()
    {
        EntityClassifier.Classify(priorHash: "sha256:abc", currentHash: "sha256:def")
            .Should().Be(ClassificationOutcome.Updated);
    }

    [Fact]
    public void Classify_CaseSensitiveHashCompare()
    {
        EntityClassifier.Classify(priorHash: "sha256:abc", currentHash: "sha256:ABC")
            .Should().Be(ClassificationOutcome.Updated);
    }
}
