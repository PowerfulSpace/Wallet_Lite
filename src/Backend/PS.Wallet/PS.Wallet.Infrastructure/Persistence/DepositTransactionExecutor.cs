using Microsoft.EntityFrameworkCore;
using PS.Wallet.Application;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.DTOs;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Infrastructure.Persistence;

internal sealed class DepositTransactionExecutor : IDepositTransactionExecutor
{
    private readonly WalletDbContext _db;
    private readonly IIdempotencyCoordinator _idempotency;

    public DepositTransactionExecutor(WalletDbContext db, IIdempotencyCoordinator idempotency)
    {
        _db = db;
        _idempotency = idempotency;
    }

    public async Task<DepositDto> ExecuteAsync(Guid userId, decimal amount, Guid idempotencyRecordId, CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        DepositDto dto;

        try
        {
            var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Wallets
SET Balance = Balance + {amount},
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE UserId = {userId}
  AND Balance + {amount} <= 1000000.00", cancellationToken);

            if (affected == 0)
            {
                throw new ConflictException("BALANCE_LIMIT_EXCEEDED", "The deposit would exceed the maximum wallet balance.");
            }

            var wallet = await _db.Wallets.AsNoTracking().FirstAsync(w => w.UserId == userId, cancellationToken);

            var now = DateTime.UtcNow;
            var deposit = Deposit.Create(wallet.Id, amount, now);
            _db.Deposits.Add(deposit);

            var ledger = LedgerEntry.ForDeposit(wallet.Id, deposit.Id, amount, now);
            _db.LedgerEntries.Add(ledger);

            dto = new DepositDto(deposit.Id, wallet.Id, amount, "USD", wallet.Balance, now);

            // The replayable result is completed inside the transaction so it commits atomically
            // with the financial effect (ADR-005 §6).
            await _idempotency.CompleteAsync(
                idempotencyRecordId, deposit.Id, 201, System.Text.Json.JsonSerializer.Serialize(dto), cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (!TransactionalOutcomePolicy.IsConfirmedFailure(ex))
        {
            throw new UncertainCommitException(
                "The deposit outcome is unknown; retry with the same idempotency key to resolve it.", ex);
        }

        return dto;
    }
}
