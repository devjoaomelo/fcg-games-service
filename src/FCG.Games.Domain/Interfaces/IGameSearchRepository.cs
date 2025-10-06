using FCG.Games.Domain.Entities;

namespace FCG.Games.Domain.Interfaces;

public interface IGameSearchRepository
{
    Task IndexAsync(Game game, CancellationToken ct = default);
    Task<(IReadOnlyList<Game> items, long total)> SearchAsync(string? q, string? genre, int page, int size, CancellationToken ct = default);
}
