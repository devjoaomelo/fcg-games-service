namespace FCG.Games.Domain.ValueObjects;

public sealed partial class GameTitle
{
    public string Value { get; }
    private GameTitle(string value) 
    {
        Value = value;
    } 

    public static GameTitle Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Title is required", nameof(value));

        var title = value.Trim();

        if (title.Length > 200) throw new ArgumentException("Title max length is 200");
        return new GameTitle(title);
    }

    public override string ToString() => Value;
    public override bool Equals(object? obj) => obj is GameTitle title && title.Value == Value;
    public override int GetHashCode() => Value.GetHashCode();
}
