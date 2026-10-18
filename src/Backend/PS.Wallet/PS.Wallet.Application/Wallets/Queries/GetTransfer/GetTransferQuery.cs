using MediatR;
using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Wallets.Queries.GetTransfer;

public sealed record GetTransferQuery(Guid TransferId) : IRequest<TransferDto>;
