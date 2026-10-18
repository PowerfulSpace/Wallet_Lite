using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PS.Wallet.Application;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.DTOs;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Infrastructure.Persistence;

internal sealed class TransferTransactionExecutor : ITransferTransactionExecutor
{
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 100;

    private readonly WalletDbContext _db;
    private readonly IIdempotencyCoordinator _idempotency;

    public TransferTransactionExecutor(WalletDbContext db, IIdempotencyCoordinator idempotency)
    {
        _db = db;
        _idempotency = idempotency;
    }

    public async Task<TransferDto> ExecuteAsync(
        Guid senderUserId,
        Guid recipientUserId,
        decimal amount,
        Guid idempotencyRecordId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await ExecuteOnceAsync(
                    senderUserId, recipientUserId, amount, idempotencyRecordId, cancellationToken);
            }
            catch (Exception ex) when (IsDeadlockVictim(ex) && attempt < MaxRetries)
            {
                _db.ChangeTracker.Clear();

                var delay = (int)(BaseDelayMs * Math.Pow(2, attempt));
                var jitter = Random.Shared.Next(-delay / 5, delay / 5 + 1);
                await Task.Delay(delay + jitter, cancellationToken);
            }
            catch (Exception ex) when (!TransactionalOutcomePolicy.IsConfirmedFailure(ex))
            {
                throw new UncertainCommitException(
                    "The transfer outcome is unknown; retry with the same idempotency key to resolve it.", ex);
            }
        }

        throw new UncertainCommitException(
            "The transfer outcome is unknown after the maximum number of deadlock retries; " +
            "retry with the same idempotency key to resolve it.");
    }

    private static bool IsDeadlockVictim(Exception exception) =>
        exception is SqlException { Number: 1205 }
        || exception is DbUpdateException { InnerException: SqlException { Number: 1205 } };

    private async Task<TransferDto> ExecuteOnceAsync(
        Guid senderUserId,
        Guid recipientUserId,
        decimal amount,
        Guid idempotencyRecordId,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var senderWallet = await _db.Wallets
            .AsNoTracking()
            .FirstAsync(w => w.UserId == senderUserId, cancellationToken);

        var recipientWallet = await _db.Wallets
            .AsNoTracking()
            .FirstAsync(w => w.UserId == recipientUserId, cancellationToken);

        // Lock ordering (CONST-021): update wallet rows in ascending Wallet.Id order.
        var orderedWalletIds = new[] { senderWallet.Id, recipientWallet.Id }
            .OrderBy(id => id, Comparer<Guid>.Default)
            .ToArray();

        foreach (var walletId in orderedWalletIds)
        {
            if (walletId == senderWallet.Id)
            {
                var debitAffected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Wallets
SET Balance = Balance - {amount},
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE Id = {walletId}
  AND Balance >= {amount}", cancellationToken);

                if (debitAffected == 0)
                {
                    throw new ConflictException("INSUFFICIENT_FUNDS", "The sender's wallet does not have sufficient funds.");
                }
            }
            else
            {
                var creditAffected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Wallets
SET Balance = Balance + {amount},
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE Id = {walletId}
  AND Balance + {amount} <= 1000000.00", cancellationToken);

                if (creditAffected == 0)
                {
                    throw new ConflictException("BALANCE_LIMIT_EXCEEDED", "The recipient's balance would exceed the maximum.");
                }
            }
        }

        var senderUser = await _db.Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == senderWallet.UserId, cancellationToken);

        var recipientUser = await _db.Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == recipientWallet.UserId, cancellationToken);

        var now = DateTime.UtcNow;
        var transfer = Transfer.Create(senderWallet.Id, recipientWallet.Id, amount, now);
        _db.Transfers.Add(transfer);

        _db.LedgerEntries.AddRange(
            LedgerEntry.ForTransfer(senderWallet.Id, transfer.Id, -amount, now),
            LedgerEntry.ForTransfer(recipientWallet.Id, transfer.Id, amount, now));

        var dto = new TransferDto(
            transfer.Id,
            senderUser.Username,
            recipientUser.Username,
            amount,
            "USD",
            now);

        // The replayable result is completed inside the transaction so it commits atomically
        // with the financial effect (ADR-005 §6).
        await _idempotency.CompleteAsync(
            idempotencyRecordId, transfer.Id, 201, System.Text.Json.JsonSerializer.Serialize(dto), cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return dto;
    }
}
