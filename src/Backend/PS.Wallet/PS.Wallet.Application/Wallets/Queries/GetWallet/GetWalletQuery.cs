using MediatR;
using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Wallets.Queries.GetWallet;

public sealed record GetWalletQuery : IRequest<WalletDto>;
