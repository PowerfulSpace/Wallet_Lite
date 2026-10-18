using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PS.Wallet.Infrastructure.Persistence;

public sealed class WalletDbContextFactory : IDesignTimeDbContextFactory<WalletDbContext>
{
    public WalletDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WALLET_DB")
            ?? "Server=localhost;Database=Wallet;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new WalletDbContext(options);
    }
}
