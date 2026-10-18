namespace PS.Wallet.Domain.Entities;

public sealed class Deposit
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Deposit() { }

    public static Deposit Create(Guid walletId, decimal amount, DateTime createdAtUtc)
    {
        if (amount <= 0m || amount > 1000000.00m)
        {
            throw new DomainException("Deposit amount must be in (0, 1000000.00].");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("Deposit amount must have at most two fractional decimal digits.");
        }

        return new Deposit
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            Amount = amount,
            CreatedAtUtc = createdAtUtc,
        };
    }
}
