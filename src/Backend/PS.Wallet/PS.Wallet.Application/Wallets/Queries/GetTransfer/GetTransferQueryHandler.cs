using MediatR;
using Microsoft.EntityFrameworkCore;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Wallets.Queries.GetTransfer;

internal sealed class GetTransferQueryHandler : IRequestHandler<GetTransferQuery, TransferDto>
{
    private readonly ICurrentUser _currentUser;
    private readonly IWalletDbContext _db;

    public GetTransferQueryHandler(ICurrentUser currentUser, IWalletDbContext db)
    {
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<TransferDto> Handle(GetTransferQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == _currentUser.Username, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == user.Id, cancellationToken)
            ?? throw new NotFoundException("Wallet not found.");

        var transfer = await _db.Transfers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == request.TransferId
                     && (t.SenderWalletId == wallet.Id || t.RecipientWalletId == wallet.Id),
                cancellationToken)
            ?? throw new NotFoundException("Transfer not found.");

        var senderWallet = await _db.Wallets
            .AsNoTracking()
            .FirstAsync(w => w.Id == transfer.SenderWalletId, cancellationToken);

        var recipientWallet = await _db.Wallets
            .AsNoTracking()
            .FirstAsync(w => w.Id == transfer.RecipientWalletId, cancellationToken);

        var senderUser = await _db.Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == senderWallet.UserId, cancellationToken);

        var recipientUser = await _db.Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == recipientWallet.UserId, cancellationToken);

        return new TransferDto(
            transfer.Id,
            senderUser.Username,
            recipientUser.Username,
            transfer.Amount,
            "USD",
            transfer.CreatedAtUtc);
    }
}
