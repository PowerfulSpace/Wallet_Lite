namespace PS.Wallet.Application.Wallets.DTOs;

public sealed record TransferDto(
    Guid TransferId,
    string SenderUsername,
    string RecipientUsername,
    decimal Amount,
    string Currency,
    DateTime CreatedAtUtc);
