namespace PS.Wallet.Domain.ValueObjects;

public readonly record struct Money
{
    public const decimal MaxValue = 1000000.00m;

    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new DomainException("Amount must be greater than zero.");
        }

        if (amount > MaxValue)
        {
            throw new DomainException($"Amount must not exceed {MaxValue}.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("Amount must have at most two fractional decimal digits.");
        }

        Amount = amount;
    }

    public static Money FromDecimal(decimal amount) => new(amount);

    public override string ToString() => Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
