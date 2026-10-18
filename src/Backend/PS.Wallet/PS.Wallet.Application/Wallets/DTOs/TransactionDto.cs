namespace PS.Wallet.Application.Wallets.DTOs;

public sealed record TransactionDto(
    Guid TransactionId,
    string Type,
    decimal Amount,
    string Currency,
    string Description,
    Guid RelatedOperationId,
    DateTime CreatedAtUtc);

public sealed record PagedTransactionsDto(
    IReadOnlyList<TransactionDto> Items,
    string? NextCursor,
    bool HasMore);
