namespace PS.Wallet.Application.Wallets.DTOs;

public sealed record WalletDto(
    Guid WalletId,
    string Currency,
    decimal Balance,
    DateTime CreatedAtUtc);
