using MediatR;
using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Wallets.Commands.CreateTransfer;

public sealed record CreateTransferCommand(
    string RecipientUsername,
    decimal Amount,
    string IdempotencyKey) : IRequest<TransferDto>;
