namespace FCG.Games.Infra.Data;

public sealed class StoredEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AggregateId { get; set; }
    public string Type { get; set; } = default!;
    public string Data { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
