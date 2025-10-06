namespace FCG.Games.Domain.Entities;

public sealed class Game
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Title { get; private set; }
    public string? Genre { get; private set; }
    public decimal Price { get; private set; }

    private Game() { }

    public Game(string title, string? genre, decimal price)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required", nameof(title));
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
        Title = title.Trim();
        Genre = string.IsNullOrWhiteSpace(genre) ? null : genre.Trim();
        Price = price;
    }
}
