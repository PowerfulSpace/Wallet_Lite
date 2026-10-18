using PS.Wallet.Domain.ValueObjects;

namespace PS.Wallet.Domain.Entities;

public sealed class WalletAccount
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Balance { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private WalletAccount() { }

    public static WalletAccount Create(Guid userId, DateTime createdAtUtc)
    {
        return new WalletAccount
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Balance = 0.00m,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };
    }
}
