using FCG.Games.Domain.Entities;
using FCG.Games.Domain.Interfaces;
using FCG.Games.Domain.ValueObjects;

namespace FCG.Games.Domain.Services;

public sealed class GameCreationService : IGameCreationService
{
    public Game Create(string title, string? description, decimal price)
    {
        var t = GameTitle.Create(title);
        var d = Description.Create(description);
        var p = Price.Parse(price);

        return new Game(t, d, p);
    }
}
