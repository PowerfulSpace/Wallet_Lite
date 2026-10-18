using MediatR;
using Microsoft.EntityFrameworkCore;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.DTOs;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Application.Wallets.Queries.GetTransactions;

internal sealed class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, PagedTransactionsDto>
{
    private readonly ICurrentUser _currentUser;
    private readonly IWalletDbContext _db;

    public GetTransactionsQueryHandler(ICurrentUser currentUser, IWalletDbContext db)
    {
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<PagedTransactionsDto> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == _currentUser.Username, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == user.Id, cancellationToken)
            ?? throw new NotFoundException("Wallet not found.");

        TransactionCursor? cursor = null;
        if (!string.IsNullOrEmpty(request.Cursor))
        {
            cursor = TransactionCursor.Decode(request.Cursor);
        }

        var limit = request.Limit + 1; // +1 for hasMore detection

        IQueryable<LedgerEntry> query = _db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.WalletId == wallet.Id)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenByDescending(e => e.Id);

        if (cursor is not null)
        {
            query = query.Where(e =>
                e.CreatedAtUtc < cursor.CreatedAtUtc ||
                (e.CreatedAtUtc == cursor.CreatedAtUtc && e.Id.CompareTo(cursor.Id) < 0));
        }

        // A single projection resolves the transfer counterparties, so no per-entry queries are issued.
        var entries = await query
            .Take(limit)
            .Select(e => new LedgerEntryRow
            {
                Id = e.Id,
                Amount = e.Amount,
                CreatedAtUtc = e.CreatedAtUtc,
                DepositId = e.DepositId,
                TransferId = e.TransferId,
                SenderUsername = e.TransferId == null
                    ? null
                    : _db.Transfers
                        .Where(t => t.Id == e.TransferId)
                        .Select(t => _db.Users
                            .Where(u => _db.Wallets.Any(w => w.Id == t.SenderWalletId && w.UserId == u.Id))
                            .Select(u => u.Username)
                            .FirstOrDefault())
                        .FirstOrDefault(),
                RecipientUsername = e.TransferId == null
                    ? null
                    : _db.Transfers
                        .Where(t => t.Id == e.TransferId)
                        .Select(t => _db.Users
                            .Where(u => _db.Wallets.Any(w => w.Id == t.RecipientWalletId && w.UserId == u.Id))
                            .Select(u => u.Username)
                            .FirstOrDefault())
                        .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var hasMore = entries.Count > request.Limit;
        if (hasMore)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        var items = entries.Select(Map).ToList();

        var nextCursor = hasMore && entries.Count > 0
            ? new TransactionCursor(entries[^1].CreatedAtUtc, entries[^1].Id).Encode()
            : null;

        return new PagedTransactionsDto(items, nextCursor, hasMore);
    }

    private static TransactionDto Map(LedgerEntryRow entry)
    {
        if (entry.DepositId is not null)
        {
            return new TransactionDto(
                entry.Id,
                "deposit",
                entry.Amount,
                "USD",
                "Virtual deposit",
                entry.DepositId.Value,
                entry.CreatedAtUtc);
        }

        if (entry.TransferId is not null)
        {
            var isOutgoing = entry.Amount < 0;
            var counterpartyUsername = isOutgoing ? entry.RecipientUsername : entry.SenderUsername;

            return new TransactionDto(
                entry.Id,
                isOutgoing ? "transfer_out" : "transfer_in",
                entry.Amount,
                "USD",
                isOutgoing
                    ? $"Transfer to {counterpartyUsername ?? "unknown"}"
                    : $"Transfer from {counterpartyUsername ?? "unknown"}",
                entry.TransferId.Value,
                entry.CreatedAtUtc);
        }

        throw new InvalidOperationException("Ledger entry must reference either a deposit or a transfer.");
    }

    private sealed class LedgerEntryRow
    {
        public Guid Id { get; init; }
        public decimal Amount { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public Guid? DepositId { get; init; }
        public Guid? TransferId { get; init; }
        public string? SenderUsername { get; init; }
        public string? RecipientUsername { get; init; }
    }
}
