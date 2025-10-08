using FCG.Games.Domain.Services;
using FluentAssertions;
using System.ComponentModel.Design.Serialization;
using Xunit;

namespace FCG.Games.UnitTests;

public class GameCreationServiceTests
{
    [Fact]
    public void Create_Should_Return_Game_With_Valid_VOs()
    {
        var svc = new GameCreationService();
        var title = "Elden Ring";
        string? description = "Action RPG";
        decimal price = 299.90m;

        var game = svc.Create(title, description, price);
        game.Should().NotBeNull();
        game.Title.Should().NotBeNull();
        game.Description.Should().NotBeNull();
        game.Price.Should().NotBeNull();
        game.Title.Value.Should().Be(title);
        game.Description.Value.Should().Be(description);
        game.Price.Value.Should().Be(price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Title_Is_NullOrWhiteSpace(string? badTitle)
    {
        var svc = new GameCreationService();
        string? description = "qualquer";
        decimal price = 10m;

        var act = () => svc.Create(badTitle!, description, price);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_Should_Throw_When_Price_Is_Negative(decimal badPrice)
    {
        var svc = new GameCreationService();
        var act = () => svc.Create("qualquer", "qualquer", badPrice);

        act.Should().Throw<ArgumentException>();
    }
}
