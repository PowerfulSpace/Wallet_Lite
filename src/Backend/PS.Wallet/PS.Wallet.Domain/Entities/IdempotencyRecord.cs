namespace PS.Wallet.Domain.Entities;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string OperationType { get; set; } = default!;
    public string IdempotencyKey { get; set; } = default!;
    public byte[] RequestFingerprint { get; set; } = default!;
    public Guid? OperationId { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? ResponsePayload { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LeaseExpiresAtUtc { get; set; }
}
