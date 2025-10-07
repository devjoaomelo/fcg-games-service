namespace FCG.Games.Domain.ValueObjects;

public readonly struct Price
{
    public decimal Value { get; }
    private Price(decimal value) => Value = value;

    public static Price Parse(decimal value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Price must be >= 0");

        if (decimal.Round(value, 2) != value) throw new ArgumentException("Price must have 2 decimals max");

        return new Price(value);
    }

    public override string ToString() => Value.ToString("0.00");
}
