using Microsoft.EntityFrameworkCore;
using NSubstitute;
using PS.Wallet.Application;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.Queries.GetWallet;
using PS.Wallet.Domain.Entities;
using PS.Wallet.UnitTests.TestSupport;
using Xunit;

namespace PS.Wallet.UnitTests.Application;

public sealed class GetWalletQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingUserAndWallet_ReturnsWalletDto()
    {
        var options = new DbContextOptionsBuilder<FakeWalletDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new FakeWalletDbContext(options);
        var user = User.Create("alice", "alice@example.com", DateTime.UtcNow);
        var wallet = WalletAccount.Create(user.Id, DateTime.UtcNow);
        db.Users.Add(user);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Username.Returns("alice");

        var handler = new GetWalletQueryHandler(currentUser, db);
        var result = await handler.Handle(new GetWalletQuery(), CancellationToken.None);

        Assert.Equal(wallet.Id, result.WalletId);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(0m, result.Balance);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var options = new DbContextOptionsBuilder<FakeWalletDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new FakeWalletDbContext(options);
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Username.Returns("ghost");

        var handler = new GetWalletQueryHandler(currentUser, db);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetWalletQuery(), CancellationToken.None));
    }
}
