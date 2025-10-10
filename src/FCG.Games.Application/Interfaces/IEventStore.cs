using FCG.Games.Application.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCG.Games.Application.Interfaces;

public interface IEventStore
{
    Task AppendAsync(Guid aggregateId, string type, object? data, CancellationToken ct = default);
    Task<IReadOnlyList<EventRecord>> ListByAggregateAsync(Guid aggregateId, CancellationToken ct = default);
}

