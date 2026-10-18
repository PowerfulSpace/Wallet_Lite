using Microsoft.EntityFrameworkCore;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.UnitTests.TestSupport;

internal sealed class FakeWalletDbContext : DbContext, IWalletDbContext
{
    public FakeWalletDbContext(DbContextOptions options) : base(options) { }

    public DbSet<WalletAccount> Wallets => Set<WalletAccount>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Deposit> Deposits => Set<Deposit>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    Task<int> IWalletDbContext.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasKey(x => x.Id);
        modelBuilder.Entity<WalletAccount>().HasKey(x => x.Id);
        modelBuilder.Entity<Deposit>().HasKey(x => x.Id);
        modelBuilder.Entity<LedgerEntry>().HasKey(x => x.Id);
        modelBuilder.Entity<Transfer>().HasKey(x => x.Id);
        modelBuilder.Entity<IdempotencyRecord>().HasKey(x => x.Id);
    }
}
