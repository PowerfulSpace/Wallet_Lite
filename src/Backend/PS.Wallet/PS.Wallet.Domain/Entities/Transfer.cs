namespace PS.Wallet.Domain.Entities;

public sealed class Transfer
{
    public Guid Id { get; private set; }
    public Guid SenderWalletId { get; private set; }
    public Guid RecipientWalletId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Transfer() { }

    public static Transfer Create(Guid senderWalletId, Guid recipientWalletId, decimal amount, DateTime createdAtUtc)
    {
        if (senderWalletId == recipientWalletId)
        {
            throw new DomainException("Sender and recipient wallets must be different.");
        }

        if (amount <= 0m || amount > 1000000.00m)
        {
            throw new DomainException("Transfer amount must be in (0, 1000000.00].");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("Transfer amount must have at most two fractional decimal digits.");
        }

        return new Transfer
        {
            Id = Guid.NewGuid(),
            SenderWalletId = senderWalletId,
            RecipientWalletId = recipientWalletId,
            Amount = amount,
            CreatedAtUtc = createdAtUtc,
        };
    }
}
