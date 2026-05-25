using BusTerminal.Api.Domain;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Domain;

// Spec 004 / T086 / FR-011.
public sealed class SemanticVersionTests
{
    [Fact]
    public void Comparable_orders_by_major_minor_patch()
    {
        var v100 = new SemanticVersion(1, 0, 0);
        var v110 = new SemanticVersion(1, 1, 0);
        var v200 = new SemanticVersion(2, 0, 0);
        var v101 = new SemanticVersion(1, 0, 1);

        v100.CompareTo(v110).Should().BeLessThan(0);
        v110.CompareTo(v100).Should().BeGreaterThan(0);
        v100.CompareTo(v200).Should().BeLessThan(0);
        v100.CompareTo(v101).Should().BeLessThan(0);
        v200.CompareTo(v110).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Equality_uses_record_default()
    {
        var a = new SemanticVersion(1, 2, 3);
        var b = new SemanticVersion(1, 2, 3);
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Carries_compatibility_and_lineage()
    {
        var history = new[]
        {
            new HistoricalVersionEntry(0, 9, 0, LifecycleState.Deprecated, ReplacedBy: new SemanticVersionRef(1, 0, 0)),
        };

        var v = new SemanticVersion(
            1, 0, 0,
            Compatibility: CompatibilityIndicator.Backward,
            CurrentVersionRef: new SemanticVersionRef(1, 0, 0),
            VersionHistory: history);

        v.Compatibility.Should().Be(CompatibilityIndicator.Backward);
        v.VersionHistory.Should().HaveCount(1);
        var entry = v.VersionHistory!.First();
        entry.Lifecycle.Should().Be(LifecycleState.Deprecated);
        entry.ReplacedBy!.ToString().Should().Be("1.0.0");
    }
}
