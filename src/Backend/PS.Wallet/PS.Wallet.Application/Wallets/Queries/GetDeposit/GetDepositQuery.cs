using MediatR;
using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Wallets.Queries.GetDeposit;

public sealed record GetDepositQuery(Guid DepositId) : IRequest<DepositDto>;
