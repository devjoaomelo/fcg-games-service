using FCG.Games.Domain.Entities;

namespace FCG.Games.Infra.Documents;

// DTO para indexação no OpenSearch
public sealed class GameDocument
{
    public Guid Id { get; init; }
    public string Title { get; init; } = default!;
    public string Description { get; init; } = default!;
    public decimal Price { get; init; }

    public static GameDocument FromDomain(Game g) => new()
    {
        Id = g.Id,
        Title = g.Title.Value,
        Description = g.Description.Value,
        Price = g.Price.Value
    };
}
