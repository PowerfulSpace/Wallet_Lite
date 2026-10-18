using MediatR;
using Microsoft.EntityFrameworkCore;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Wallets.Queries.GetWallet;

internal sealed class GetWalletQueryHandler : IRequestHandler<GetWalletQuery, WalletDto>
{
    private readonly ICurrentUser _currentUser;
    private readonly IWalletDbContext _db;

    public GetWalletQueryHandler(ICurrentUser currentUser, IWalletDbContext db)
    {
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<WalletDto> Handle(GetWalletQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == _currentUser.Username, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == user.Id, cancellationToken);

        if (wallet is null)
        {
            throw new NotFoundException("Wallet not found.");
        }

        return new WalletDto(wallet.Id, "USD", wallet.Balance, wallet.CreatedAtUtc);
    }
}
