using BusTerminal.Api.Domain;
using FluentAssertions;

namespace BusTerminal.Api.Tests.Unit.Domain;

// Spec 004 / T084 / FR-022.
public sealed class ResourceNameTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("payments")]
    [InlineData("payments-platform")]
    [InlineData("a1")]
    [InlineData("0-9")]
    [InlineData("foo-bar-baz")]
    public void Valid_names_construct(string name)
    {
        var resourceName = new ResourceName(name);
        resourceName.Value.Should().Be(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Payments")] // uppercase
    [InlineData("payments ")] // trailing space
    [InlineData(" payments")] // leading space
    [InlineData("payments platform")] // space inside
    [InlineData("payments_platform")] // underscore
    [InlineData("-payments")] // leading hyphen
    [InlineData("payments-")] // trailing hyphen
    [InlineData("payments--platform")] // double hyphen
    public void Invalid_names_throw(string name)
    {
        var act = () => new ResourceName(name);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Implicit_string_conversion_returns_value()
    {
        string s = new ResourceName("payments");
        s.Should().Be("payments");
    }
}
