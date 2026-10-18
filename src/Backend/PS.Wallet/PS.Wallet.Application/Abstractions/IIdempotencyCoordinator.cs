namespace PS.Wallet.Application.Abstractions;

public interface IIdempotencyCoordinator
{
    Task<IdempotencyLookupResult> LookupOrReserveAsync(
        Guid userId,
        string operationType,
        string idempotencyKey,
        byte[] fingerprint,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        Guid idempotencyRecordId,
        Guid operationId,
        int statusCode,
        string payload,
        CancellationToken cancellationToken);

    Task ReleaseAsync(Guid idempotencyRecordId, CancellationToken cancellationToken);
}

public sealed record IdempotencyLookupResult(
    IdempotencyLookupStatus Status,
    Guid? RecordId = null,
    int? StoredStatusCode = null,
    string? StoredPayload = null);

public enum IdempotencyLookupStatus
{
    Reserved,
    Replay,
    Conflict,
    InProgress,
}
