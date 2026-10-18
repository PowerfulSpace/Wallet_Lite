using MediatR;
using Microsoft.EntityFrameworkCore;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Wallets.Queries.GetDeposit;

internal sealed class GetDepositQueryHandler : IRequestHandler<GetDepositQuery, DepositDto>
{
    private readonly ICurrentUser _currentUser;
    private readonly IWalletDbContext _db;

    public GetDepositQueryHandler(ICurrentUser currentUser, IWalletDbContext db)
    {
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<DepositDto> Handle(GetDepositQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == _currentUser.Username, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == user.Id, cancellationToken)
            ?? throw new NotFoundException("Wallet not found.");

        var deposit = await _db.Deposits
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DepositId && d.WalletId == wallet.Id, cancellationToken)
            ?? throw new NotFoundException("Deposit not found.");

        return new DepositDto(
            deposit.Id,
            deposit.WalletId,
            deposit.Amount,
            "USD",
            wallet.Balance,
            deposit.CreatedAtUtc);
    }
}
