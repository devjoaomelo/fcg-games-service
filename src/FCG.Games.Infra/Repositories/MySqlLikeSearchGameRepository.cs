using FCG.Games.Domain.Entities;
using FCG.Games.Domain.Interfaces;
using FCG.Games.Domain.Models;
using FCG.Games.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace FCG.Games.Infra.Search;

public sealed class MySqlLikeSearchGameRepository : IGameSearchRepository
{
    private readonly GamesDbContext _db;

    public MySqlLikeSearchGameRepository(GamesDbContext db)
    {
        _db = db;
    }

    // ==== Indexação vira no-op no fallback MySQL ====
    public Task IndexAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
    public Task BulkIndexAsync(IEnumerable<Game> games, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteByIdAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;

    // ==== Busca por LIKE no MySQL ====
    public async Task<(IReadOnlyList<Game> items, long total)> SearchAsync(
        string? q, int page, int size, CancellationToken ct = default)
    {
        if (page <= 0) page = 1;
        if (size <= 0 || size > 200) size = 10;

        var query = _db.Games.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q}%";
            query = query.Where(g =>
                EF.Functions.Like(g.Title.Value, like) ||
                EF.Functions.Like(g.Description.Value, like));
        }

        var total = await query.LongCountAsync(ct);

        var items = await query
            .OrderBy(g => g.Title.Value)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return (items, total);
    }

    // ==== Métricas com buckets de preço ====
    public async Task<GameMetrics> GetMetricsAsync(CancellationToken ct)
    {
        var q = _db.Games.AsNoTracking();

        var count = await q.LongCountAsync(ct);

        // se não houver registros, devolve métricas vazias
        if (count == 0)
        {
            return new GameMetrics(
                Count: 0,
                AvgPrice: null,
                MinPrice: null,
                MaxPrice: null,
                Buckets: Array.Empty<PriceBucket>()
            );
        }

        // agrega valores básicos
        var min = await q.Select(g => g.Price.Value).MinAsync(ct);
        var max = await q.Select(g => g.Price.Value).MaxAsync(ct);
        var avg = await q.Select(g => g.Price.Value).AverageAsync(ct);

        // Buckets fixos (simples e útil pra demo):
        // [0–49.99], [50–99.99], [100–199.99], [200+]
        // (ajuste os intervalos se precisar)
        var prices = q.Select(g => g.Price.Value);

        // traduz para CASE no MySQL
        var bucketRows = await prices
            .Select(p => new
            {
                From = p < 50m ? 0m :
                       p < 100m ? 50m :
                       p < 200m ? 100m : 200m,
                To = p < 50m ? 50m :
                       p < 100m ? 100m :
                       p < 200m ? 200m : decimal.MaxValue
            })
            .GroupBy(x => new { x.From, x.To })
            .Select(g => new
            {
                g.Key.From,
                g.Key.To,
                DocCount = (long)g.Count()
            })
            .OrderBy(b => b.From)
            .ToListAsync(ct);

        // garante a presença de todos os buckets (mesmo que 0)
        var allBuckets = new (decimal From, decimal To)[]
        {
            (0m, 50m),
            (50m, 100m),
            (100m, 200m),
            (200m, decimal.MaxValue) // representando "200+"
        };

        var buckets = allBuckets
            .Select(def =>
            {
                var row = bucketRows.FirstOrDefault(b => b.From == def.From && b.To == def.To);
                return new PriceBucket(def.From, def.To, row?.DocCount ?? 0L);
            })
            .ToList();

        return new GameMetrics(
            Count: count,
            AvgPrice: avg,
            MinPrice: min,
            MaxPrice: max,
            Buckets: buckets
        );
    }
}
