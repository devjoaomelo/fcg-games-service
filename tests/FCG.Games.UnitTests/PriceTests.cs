using FCG.Games.Domain.ValueObjects;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace FCG.Games.UnitTests;

public class PriceTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(-100.55)]
    public void Parse_Should_Throw_When_Negative(decimal badPrice)
    {
        var act = () => Price.Parse(badPrice);
        act.Should().Throw<ArgumentException>(); 
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(10)]
    [InlineData(299.90)]
    public void Parse_Should_Return_Value_When_Positive(decimal goodPrice)
    {
        var p = Price.Parse(goodPrice);
        p.Should().NotBeNull();
        p.Value.Should().Be(goodPrice);
    }
}
