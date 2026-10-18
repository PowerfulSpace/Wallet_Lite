using Microsoft.EntityFrameworkCore;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Application.Abstractions;

public interface IWalletDbContext
{
    DbSet<WalletAccount> Wallets { get; }
    DbSet<User> Users { get; }
    DbSet<Deposit> Deposits { get; }
    DbSet<Transfer> Transfers { get; }
    DbSet<LedgerEntry> LedgerEntries { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
