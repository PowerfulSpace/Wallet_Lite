using Microsoft.EntityFrameworkCore;
using PS.Wallet.Domain.Entities;

namespace PS.Wallet.Infrastructure.Persistence.Seed;

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(WalletDbContext db, CancellationToken cancellationToken = default)
    {
        await SeedUserAsync(db, "alice", "alice@example.com", cancellationToken);
        await SeedUserAsync(db, "bob", "bob@example.com", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedUserAsync(WalletDbContext db, string username, string email, CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(u => u.Username == username, cancellationToken);
        if (exists)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var user = User.Create(username, email, now);
        var wallet = WalletAccount.Create(user.Id, now);

        db.Users.Add(user);
        db.Wallets.Add(wallet);
    }
}
