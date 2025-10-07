using FCG.Games.Domain.ValueObjects;

namespace FCG.Games.Domain.Entities;

public sealed class Game
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public GameTitle Title { get; private set; }
    public Description Description { get; private set; }
    public Price Price { get; private set; }

    private Game() { }

    public Game(GameTitle title, Description description, Price price)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Price = price;
    }

    public void Update(GameTitle title, Description description, Price price)
    {
        Title = title;
        Description = description;
        Price = price;
    }
}
