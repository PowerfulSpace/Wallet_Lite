using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Abstractions;

public interface ITransferTransactionExecutor
{
    Task<TransferDto> ExecuteAsync(
        Guid senderUserId,
        Guid recipientUserId,
        decimal amount,
        Guid idempotencyRecordId,
        CancellationToken cancellationToken);
}
