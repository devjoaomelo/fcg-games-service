using FCG.Games.Domain.Entities;

namespace FCG.Games.Domain.Interfaces;

public interface IGameRepository
{
    Task AddAsync(Game game, CancellationToken ct = default);
    Task<bool> ExistsByTitleAsync(string title, CancellationToken ct = default);
    Task UpdateAsync(Game game, CancellationToken ct = default);
    Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> ListAsync(int page, int size, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
