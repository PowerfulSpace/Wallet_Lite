using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using PS.Wallet.Infrastructure.Persistence;

namespace PS.Wallet.IntegrationTests.Endpoints;

public sealed class WalletApiFactory : WebApplicationFactory<Program>
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("WALLET_TEST_DB")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=Wallet_Test;Trusted_Connection=True;TrustServerCertificate=True;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Wallet", ConnectionString);

        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        using var db = new WalletDbContext(options);
        db.Database.EnsureDeleted();
        db.Database.Migrate();
    }
}
