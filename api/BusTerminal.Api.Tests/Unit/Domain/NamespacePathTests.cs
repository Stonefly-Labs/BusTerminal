using BusTerminal.Api.Domain;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Domain;

// Spec 004 / T085 / FR-003.
public sealed class NamespacePathTests
{
    [Theory]
    [InlineData("enterprise", true)]
    [InlineData("enterprise/payments", false)]
    [InlineData("enterprise/payments/order-processing", false)]
    public void IsRoot_distinguishes_single_segment(string path, bool expectedRoot)
    {
        new NamespacePath(path).IsRoot.Should().Be(expectedRoot);
    }

    [Fact]
    public void Segments_splits_on_slash()
    {
        var path = new NamespacePath("enterprise/payments/order-processing");
        path.Segments.Should().BeEquivalentTo(["enterprise", "payments", "order-processing"]);
    }

    [Fact]
    public void Parent_of_root_is_null()
    {
        var path = new NamespacePath("enterprise");
        path.Parent.Should().BeNull();
    }

    [Fact]
    public void Parent_walks_chain()
    {
        var leaf = new NamespacePath("enterprise/payments/order-processing");
        var mid = leaf.Parent;
        mid.Should().NotBeNull();
        mid!.Value.Value.Should().Be("enterprise/payments");

        var root = mid.Value.Parent;
        root.Should().NotBeNull();
        root!.Value.Value.Should().Be("enterprise");

        root.Value.Parent.Should().BeNull();
    }

    [Fact]
    public void Append_extends_path()
    {
        var path = new NamespacePath("enterprise/payments");
        path.Append("order-processing").Value.Should().Be("enterprise/payments/order-processing");
    }

    [Theory]
    [InlineData("Enterprise/payments")] // segment uppercase
    [InlineData("enterprise/Payments")] // segment uppercase
    [InlineData("enterprise/payments order")] // space
    [InlineData("enterprise//payments")] // empty segment
    [InlineData("enterprise/-payments")] // leading hyphen
    public void Invalid_segments_throw(string path)
    {
        var act = () => new NamespacePath(path);
        act.Should().Throw<ArgumentException>();
    }
}
