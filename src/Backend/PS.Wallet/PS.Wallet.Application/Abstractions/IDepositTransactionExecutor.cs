using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Abstractions;

public interface IDepositTransactionExecutor
{
    Task<DepositDto> ExecuteAsync(
        Guid userId,
        decimal amount,
        Guid idempotencyRecordId,
        CancellationToken cancellationToken);
}
