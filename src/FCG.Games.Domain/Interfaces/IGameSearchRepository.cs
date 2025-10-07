using FCG.Games.Domain.Entities;

namespace FCG.Games.Domain.Interfaces;

public interface IGameSearchRepository
{
    Task IndexAsync(Game game, CancellationToken ct = default);
    Task<(IReadOnlyList<Game> items, long total)> SearchAsync(string? q, int page, int size, CancellationToken ct = default);
    Task DeleteByIdAsync(Guid id, CancellationToken ct = default);
}
