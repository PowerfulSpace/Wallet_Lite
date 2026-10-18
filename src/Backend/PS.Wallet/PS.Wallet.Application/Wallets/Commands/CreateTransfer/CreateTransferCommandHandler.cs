using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.DTOs;
using PS.Wallet.Domain;

namespace PS.Wallet.Application.Wallets.Commands.CreateTransfer;

internal sealed class CreateTransferCommandHandler : IRequestHandler<CreateTransferCommand, TransferDto>
{
    private const string OperationType = "transfer";

    private readonly ICurrentUser _currentUser;
    private readonly IWalletDbContext _db;
    private readonly ITransferTransactionExecutor _executor;
    private readonly IIdempotencyCoordinator _idempotency;

    public CreateTransferCommandHandler(
        ICurrentUser currentUser,
        IWalletDbContext db,
        ITransferTransactionExecutor executor,
        IIdempotencyCoordinator idempotency)
    {
        _currentUser = currentUser;
        _db = db;
        _executor = executor;
        _idempotency = idempotency;
    }

    public async Task<TransferDto> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
    {
        var sender = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == _currentUser.Username, cancellationToken)
            ?? throw new NotFoundException("Sender not found.");

        var recipientUsername = request.RecipientUsername.ToLowerInvariant();

        var recipient = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == recipientUsername, cancellationToken)
            ?? throw new NotFoundException("Recipient not found.");

        if (sender.Id == recipient.Id)
        {
            throw new ConflictException("SELF_TRANSFER", "Sender and recipient must be different users.");
        }

        var fingerprint = ComputeFingerprint(OperationType, recipient.Username, request.Amount);

        var lookup = await _idempotency.LookupOrReserveAsync(
            sender.Id, OperationType, request.IdempotencyKey, fingerprint, cancellationToken);

        switch (lookup.Status)
        {
            case IdempotencyLookupStatus.Conflict:
                throw new ConflictException(
                    "IDEMPOTENCY_KEY_REUSED",
                    "Idempotency key was reused with a different request.");

            case IdempotencyLookupStatus.InProgress:
                throw new ConflictException(
                    "IDEMPOTENCY_IN_PROGRESS",
                    "Original request is still in progress.");

            case IdempotencyLookupStatus.Replay:
                return Deserialize(lookup.StoredPayload);
        }

        var idempotencyRecordId = lookup.RecordId
            ?? throw new InvalidOperationException("Idempotency reservation did not return a record identifier.");

        try
        {
            var dto = await _executor.ExecuteAsync(
                sender.Id, recipient.Id, request.Amount, idempotencyRecordId, cancellationToken);

            // Confirmation only: the executor completes the record atomically with the financial effect.
            await _idempotency.CompleteAsync(
                idempotencyRecordId, dto.TransferId, 201, JsonSerializer.Serialize(dto), cancellationToken);

            return dto;
        }
        catch (Exception ex) when (ex is ConflictException or ValidationAppException or DomainException)
        {
            await _idempotency.ReleaseAsync(idempotencyRecordId, cancellationToken);
            throw;
        }
    }

    private static TransferDto Deserialize(string? payload) =>
        payload is null
            ? throw new InvalidOperationException("Stored idempotency response is missing.")
            : JsonSerializer.Deserialize<TransferDto>(payload)
                ?? throw new InvalidOperationException("Stored idempotency response could not be deserialized.");

    private static byte[] ComputeFingerprint(string operationType, string canonicalUsername, decimal amount)
    {
        var canonicalAmount = amount.ToString("0.00", CultureInfo.InvariantCulture);
        var input = $"{operationType}\n{canonicalUsername}\n{canonicalAmount}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }
}
