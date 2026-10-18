using MediatR;
using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Wallets.Commands.CreateDeposit;

public sealed record CreateDepositCommand(
    decimal Amount,
    string IdempotencyKey) : IRequest<DepositDto>;
