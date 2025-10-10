using FCG.Games.Application.Events;
using FCG.Games.Application.Interfaces;
using FCG.Games.Infra.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FCG.Games.Infra.Events;

public sealed class EfEventStore<TDbContext>(TDbContext db) : IEventStore
    where TDbContext : DbContext
{
    public async Task AppendAsync(Guid aggregateId, string type, object? data, CancellationToken ct = default)
    {
        var ev = new StoredEvent
        {
            Id = Guid.NewGuid(),
            AggregateId = aggregateId,
            Type = type,
            Data = JsonSerializer.Serialize(data),
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Set<StoredEvent>().Add(ev);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EventRecord>> ListByAggregateAsync(Guid aggregateId, CancellationToken ct = default)
    {
        var list = await db.Set<StoredEvent>()
            .AsNoTracking()
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.CreatedAtUtc)
            .Select(e => new EventRecord(e.Id, e.AggregateId, e.Type, e.Data, e.CreatedAtUtc))
            .ToListAsync(ct);

        return list;
    }
}
