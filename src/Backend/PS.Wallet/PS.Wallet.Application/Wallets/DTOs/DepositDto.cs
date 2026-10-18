namespace PS.Wallet.Application.Wallets.DTOs;

public sealed record DepositDto(
    Guid DepositId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    decimal BalanceAfter,
    DateTime CreatedAtUtc);
