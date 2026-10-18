namespace PS.Wallet.Domain.Entities;

public sealed class LedgerEntry
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public Guid? DepositId { get; private set; }
    public Guid? TransferId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private LedgerEntry() { }

    public static LedgerEntry ForDeposit(Guid walletId, Guid depositId, decimal amount, DateTime createdAtUtc)
    {
        if (amount <= 0m || amount > 1000000.00m)
        {
            throw new DomainException("Ledger entry amount must be in (0, 1000000.00].");
        }

        return new LedgerEntry
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            DepositId = depositId,
            TransferId = null,
            Amount = amount,
            CreatedAtUtc = createdAtUtc,
        };
    }

    public static LedgerEntry ForTransfer(Guid walletId, Guid transferId, decimal amount, DateTime createdAtUtc)
    {
        if (amount == 0m || Math.Abs(amount) > 1000000.00m)
        {
            throw new DomainException("Ledger entry amount must be non-zero and within ±1000000.00.");
        }

        return new LedgerEntry
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            DepositId = null,
            TransferId = transferId,
            Amount = amount,
            CreatedAtUtc = createdAtUtc,
        };
    }
}
