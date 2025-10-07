using FCG.Games.Domain.Entities;
using FCG.Games.Domain.Interfaces;
using FCG.Games.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace FCG.Games.Infra.Repositories;

public sealed class MySqlGameRepository(GamesDbContext db) : IGameRepository
{
    public async Task AddAsync(Game game, CancellationToken ct = default)
    {
        await db.Games.AddAsync(game, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Game game, CancellationToken ct = default)
    {
        db.Games.Update(game);
        await db.SaveChangesAsync(ct);
    }

    public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IReadOnlyList<Game>> ListAsync(int page, int size, CancellationToken ct = default)
        => await db.Games.AsNoTracking()
            .OrderBy(g => g.Title)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Games.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (entity is null) return;
        db.Games.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
