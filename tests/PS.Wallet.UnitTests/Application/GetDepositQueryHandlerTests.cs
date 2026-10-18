using Microsoft.EntityFrameworkCore;
using NSubstitute;
using PS.Wallet.Application;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.Queries.GetDeposit;
using PS.Wallet.Domain.Entities;
using PS.Wallet.UnitTests.TestSupport;
using Xunit;

namespace PS.Wallet.UnitTests.Application;

public sealed class GetDepositQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingDeposit_ReturnsDepositDto()
    {
        await using var db = CreateContext();
        var wallet = SeedUserWithWallet(db, "alice");
        var deposit = Deposit.Create(wallet.Id, 50.00m, new DateTime(2026, 10, 18, 10, 15, 0, DateTimeKind.Utc));
        db.Deposits.Add(deposit);
        await db.SaveChangesAsync();

        var handler = new GetDepositQueryHandler(CurrentUser("alice"), db);

        var result = await handler.Handle(new GetDepositQuery(deposit.Id), CancellationToken.None);

        Assert.Equal(deposit.Id, result.DepositId);
        Assert.Equal(wallet.Id, result.WalletId);
        Assert.Equal(50.00m, result.Amount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(0.00m, result.BalanceAfter);
        Assert.Equal(deposit.CreatedAtUtc, result.CreatedAtUtc);
    }

    [Fact]
    public async Task Handle_UnknownDeposit_ThrowsNotFound()
    {
        await using var db = CreateContext();
        SeedUserWithWallet(db, "alice");
        await db.SaveChangesAsync();

        var handler = new GetDepositQueryHandler(CurrentUser("alice"), db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetDepositQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DepositOfAnotherUser_ThrowsNotFound()
    {
        await using var db = CreateContext();
        SeedUserWithWallet(db, "alice");
        var bobWallet = SeedUserWithWallet(db, "bob");
        var bobDeposit = Deposit.Create(bobWallet.Id, 25.00m, DateTime.UtcNow);
        db.Deposits.Add(bobDeposit);
        await db.SaveChangesAsync();

        var handler = new GetDepositQueryHandler(CurrentUser("alice"), db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetDepositQuery(bobDeposit.Id), CancellationToken.None));
    }

    private static FakeWalletDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FakeWalletDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static WalletAccount SeedUserWithWallet(FakeWalletDbContext db, string username)
    {
        var user = User.Create(username, $"{username}@example.com", DateTime.UtcNow);
        var wallet = WalletAccount.Create(user.Id, DateTime.UtcNow);
        db.Users.Add(user);
        db.Wallets.Add(wallet);
        return wallet;
    }

    private static ICurrentUser CurrentUser(string username)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Username.Returns(username);
        return currentUser;
    }
}
