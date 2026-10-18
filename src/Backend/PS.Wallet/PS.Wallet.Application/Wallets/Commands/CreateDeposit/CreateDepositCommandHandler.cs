using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.DTOs;
using PS.Wallet.Domain;

namespace PS.Wallet.Application.Wallets.Commands.CreateDeposit;

internal sealed class CreateDepositCommandHandler : IRequestHandler<CreateDepositCommand, DepositDto>
{
    private const string OperationType = "deposit";
    private const string NoRecipient = "-";

    private readonly ICurrentUser _currentUser;
    private readonly IWalletDbContext _db;
    private readonly IDepositTransactionExecutor _executor;
    private readonly IIdempotencyCoordinator _idempotency;

    public CreateDepositCommandHandler(
        ICurrentUser currentUser,
        IWalletDbContext db,
        IDepositTransactionExecutor executor,
        IIdempotencyCoordinator idempotency)
    {
        _currentUser = currentUser;
        _db = db;
        _executor = executor;
        _idempotency = idempotency;
    }

    public async Task<DepositDto> Handle(CreateDepositCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == _currentUser.Username, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var fingerprint = ComputeFingerprint(OperationType, NoRecipient, request.Amount);

        var lookup = await _idempotency.LookupOrReserveAsync(
            user.Id, OperationType, request.IdempotencyKey, fingerprint, cancellationToken);

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
            var dto = await _executor.ExecuteAsync(user.Id, request.Amount, idempotencyRecordId, cancellationToken);

            // Confirmation only: the executor completes the record atomically with the financial effect.
            await _idempotency.CompleteAsync(
                idempotencyRecordId, dto.DepositId, 201, JsonSerializer.Serialize(dto), cancellationToken);

            return dto;
        }
        catch (Exception ex) when (ex is ConflictException or ValidationAppException or DomainException)
        {
            await _idempotency.ReleaseAsync(idempotencyRecordId, cancellationToken);
            throw;
        }
    }

    private static DepositDto Deserialize(string? payload) =>
        payload is null
            ? throw new InvalidOperationException("Stored idempotency response is missing.")
            : JsonSerializer.Deserialize<DepositDto>(payload)
                ?? throw new InvalidOperationException("Stored idempotency response could not be deserialized.");

    private static byte[] ComputeFingerprint(string operationType, string canonicalUsername, decimal amount)
    {
        var canonicalAmount = amount.ToString("0.00", CultureInfo.InvariantCulture);
        var input = $"{operationType}\n{canonicalUsername}\n{canonicalAmount}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }
}
