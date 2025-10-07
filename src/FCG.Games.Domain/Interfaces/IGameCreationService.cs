using FCG.Games.Domain.Entities;

namespace FCG.Games.Domain.Interfaces;

public interface IGameCreationService
{
    Game Create(string title, string? description, decimal price);
}