namespace FCG.Games.Application.Events;

public sealed record EventRecord(
    Guid Id,
    Guid AggregateId,
    string Type,
    string? Data,
    DateTime CreatedAtUtc
);
