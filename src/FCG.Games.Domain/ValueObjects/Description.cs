namespace FCG.Games.Domain.ValueObjects;

public sealed class Description
{
    public string Value { get; }

    private Description(string value)
    {
        Value = value;
    }

    public static Description Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Description is required", nameof(value));

        var trimmed = value.Trim();
        if (trimmed.Length < 10)
            throw new ArgumentException("Description must have at least 10 characters");
        if (trimmed.Length > 1000)
            throw new ArgumentException("Description cannot exceed 1000 characters");

        return new Description(trimmed);
    }

    public override string ToString() => Value;
    public override bool Equals(object? obj) => obj is Description description && description.Value == Value;
    public override int GetHashCode() => Value.GetHashCode();
}
