using FCG.Games.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace FCG.Games.UnitTests;

public class DescriptionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_NullOrWhiteSpace(string? bad)
    {
        var act = () => Description.Create(bad);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("123456789")]  
    public void Create_Should_Throw_When_Less_Than_10(string tooShort)
    {
        var act = () => Description.Create(tooShort);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Should_Throw_When_Greater_Than_1000()
    {
        var tooLong = new string('a', 1001);
        var act = () => Description.Create(tooLong);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(1000)]
    public void Create_Should_Return_When_Length_Between_10_And_1000(int len)
    {
        var ok = new string('a', len);
        var d = Description.Create(ok);
        d.Should().NotBeNull();
        d.Value.Should().Be(ok);
    }

    [Fact]
    public void Create_Should_Trim_Before_Validate()
    {
        var input = "   abcdefghij   "; 
        var d = Description.Create(input);
        d.Value.Should().Be("abcdefghij");
    }
}
