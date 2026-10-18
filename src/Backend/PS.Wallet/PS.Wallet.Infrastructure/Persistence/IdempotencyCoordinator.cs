using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Infrastructure.Persistence;

internal sealed class IdempotencyCoordinator : IIdempotencyCoordinator
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30); // CONST-012
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(3); // CONST-011
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100); // CONST-014

    private readonly WalletDbContext _db;

    public IdempotencyCoordinator(WalletDbContext db)
    {
        _db = db;
    }

    public async Task<IdempotencyLookupResult> LookupOrReserveAsync(
        Guid userId,
        string operationType,
        string idempotencyKey,
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        var waitDeadline = DateTime.UtcNow + BoundedWait;

        while (true)
        {
            var record = await FindAsync(userId, operationType, idempotencyKey, cancellationToken);

            if (record is null)
            {
                var reservedId = await TryReserveAsync(
                    userId, operationType, idempotencyKey, fingerprint, cancellationToken);

                if (reservedId is not null)
                {
                    return new IdempotencyLookupResult(IdempotencyLookupStatus.Reserved, reservedId.Value);
                }

                // Another request reserved the key first; re-evaluate the stored record.
                continue;
            }

            var observed = Classify(record, fingerprint);
            if (observed.Status != IdempotencyLookupStatus.InProgress)
            {
                return observed;
            }

            if (record.LeaseExpiresAtUtc <= DateTime.UtcNow)
            {
                // The lease expired without a durable outcome (KI-003); attempt to claim it for recovery.
                if (await TryClaimAsync(record.Id, cancellationToken))
                {
                    return new IdempotencyLookupResult(IdempotencyLookupStatus.Reserved, record.Id);
                }

                // Another request won the claim; re-evaluate the stored record.
                continue;
            }

            var waited = await WaitForOutcomeAsync(
                userId, operationType, idempotencyKey, fingerprint, waitDeadline, cancellationToken);

            if (waited is not null)
            {
                return waited;
            }
        }
    }

    public async Task CompleteAsync(
        Guid idempotencyRecordId,
        Guid operationId,
        int statusCode,
        string payload,
        CancellationToken cancellationToken)
    {
        // Guarded so that completion is idempotent and never overwrites a terminal outcome.
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IdempotencyRecords
SET OperationId = {operationId},
    ResponseStatusCode = {statusCode},
    ResponsePayload = {payload}
WHERE Id = {idempotencyRecordId}
  AND ResponseStatusCode IS NULL", cancellationToken);
    }

    public async Task ReleaseAsync(Guid idempotencyRecordId, CancellationToken cancellationToken)
    {
        // CONST-015: a business rejection releases the key for a fresh attempt.
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
DELETE FROM IdempotencyRecords
WHERE Id = {idempotencyRecordId}
  AND ResponseStatusCode IS NULL", cancellationToken);
    }

    private async Task<IdempotencyLookupResult?> WaitForOutcomeAsync(
        Guid userId,
        string operationType,
        string idempotencyKey,
        byte[] fingerprint,
        DateTime waitDeadline,
        CancellationToken cancellationToken)
    {
        while (DateTime.UtcNow < waitDeadline)
        {
            await Task.Delay(PollInterval, cancellationToken);

            var record = await FindAsync(userId, operationType, idempotencyKey, cancellationToken);

            if (record is null)
            {
                // The in-progress record was released; re-evaluate from scratch.
                return null;
            }

            var observed = Classify(record, fingerprint);
            if (observed.Status != IdempotencyLookupStatus.InProgress)
            {
                return observed;
            }

            if (record.LeaseExpiresAtUtc <= DateTime.UtcNow)
            {
                // The lease expired while waiting; re-evaluate so the record can be claimed.
                return null;
            }
        }

        return new IdempotencyLookupResult(IdempotencyLookupStatus.InProgress);
    }

    private static IdempotencyLookupResult Classify(IdempotencyRecord record, byte[] fingerprint)
    {
        if (!record.RequestFingerprint.SequenceEqual(fingerprint))
        {
            return new IdempotencyLookupResult(IdempotencyLookupStatus.Conflict);
        }

        if (record.ResponseStatusCode is not null && record.ResponsePayload is not null)
        {
            return new IdempotencyLookupResult(
                IdempotencyLookupStatus.Replay,
                record.Id,
                record.ResponseStatusCode,
                record.ResponsePayload);
        }

        return new IdempotencyLookupResult(IdempotencyLookupStatus.InProgress, record.Id);
    }

    private Task<IdempotencyRecord?> FindAsync(
        Guid userId,
        string operationType,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        _db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.UserId == userId
                    && r.OperationType == operationType
                    && r.IdempotencyKey == idempotencyKey,
                cancellationToken);

    private async Task<Guid?> TryReserveAsync(
        Guid userId,
        string operationType,
        string idempotencyKey,
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        var recordId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        try
        {
            // Raw insert so that the reservation commits immediately and is not buffered by the change tracker.
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO IdempotencyRecords
    (Id, UserId, OperationType, IdempotencyKey, RequestFingerprint, OperationId, ResponseStatusCode, ResponsePayload, CreatedAtUtc, LeaseExpiresAtUtc)
VALUES
    ({recordId}, {userId}, {operationType}, {idempotencyKey}, {fingerprint}, NULL, NULL, NULL, {now}, {now.Add(LeaseDuration)})",
                cancellationToken);

            return recordId;
        }
        catch (Exception ex) when (IsUniqueKeyViolation(ex))
        {
            return null;
        }
    }

    private async Task<bool> TryClaimAsync(Guid idempotencyRecordId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IdempotencyRecords
SET LeaseExpiresAtUtc = {now.Add(LeaseDuration)}
WHERE Id = {idempotencyRecordId}
  AND ResponseStatusCode IS NULL
  AND LeaseExpiresAtUtc < {now}", cancellationToken);

        return affected == 1;
    }

    private static bool IsUniqueKeyViolation(Exception exception) =>
        exception is SqlException { Number: 2601 or 2627 }
        || exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } };
}
