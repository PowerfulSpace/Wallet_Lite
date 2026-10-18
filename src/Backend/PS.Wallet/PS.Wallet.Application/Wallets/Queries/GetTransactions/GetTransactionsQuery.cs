using MediatR;
using PS.Wallet.Application.Wallets.DTOs;

namespace PS.Wallet.Application.Wallets.Queries.GetTransactions;

public sealed record GetTransactionsQuery(string? Cursor, int Limit) : IRequest<PagedTransactionsDto>;
