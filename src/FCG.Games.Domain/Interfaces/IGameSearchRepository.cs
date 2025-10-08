using FCG.Games.Domain.Entities;
using FCG.Games.Domain.Models;

namespace FCG.Games.Domain.Interfaces;

public interface IGameSearchRepository
{
    Task IndexAsync(Game game, CancellationToken ct = default);
    Task<(IReadOnlyList<Game> items, long total)> SearchAsync(string? q, int page, int size, CancellationToken ct = default);
    Task DeleteByIdAsync(Guid id, CancellationToken ct = default);
    Task<GameMetrics> GetMetricsAsync(CancellationToken ct);
    Task BulkIndexAsync(IEnumerable<Game> games, CancellationToken ct);
}
