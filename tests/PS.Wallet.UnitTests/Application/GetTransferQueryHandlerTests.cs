using Microsoft.EntityFrameworkCore;
using NSubstitute;
using PS.Wallet.Application;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.Queries.GetTransfer;
using PS.Wallet.Domain.Entities;
using PS.Wallet.UnitTests.TestSupport;
using Xunit;

namespace PS.Wallet.UnitTests.Application;

public sealed class GetTransferQueryHandlerTests
{
    [Fact]
    public async Task Handle_CurrentUserIsSender_ReturnsTransfer()
    {
        await using var db = CreateContext();
        var transfer = SeedTransfer(db, out var senderUsername, out var recipientUsername);

        var handler = new GetTransferQueryHandler(CurrentUser(senderUsername), db);

        var result = await handler.Handle(new GetTransferQuery(transfer.Id), CancellationToken.None);

        Assert.Equal(transfer.Id, result.TransferId);
        Assert.Equal(senderUsername, result.SenderUsername);
        Assert.Equal(recipientUsername, result.RecipientUsername);
        Assert.Equal(transfer.Amount, result.Amount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(transfer.CreatedAtUtc, result.CreatedAtUtc);
    }

    [Fact]
    public async Task Handle_CurrentUserIsRecipient_ReturnsTransfer()
    {
        await using var db = CreateContext();
        var transfer = SeedTransfer(db, out var senderUsername, out var recipientUsername);

        var handler = new GetTransferQueryHandler(CurrentUser(recipientUsername), db);

        var result = await handler.Handle(new GetTransferQuery(transfer.Id), CancellationToken.None);

        Assert.Equal(transfer.Id, result.TransferId);
    }

    [Fact]
    public async Task Handle_CurrentUserIsNeitherParticipant_ThrowsNotFound()
    {
        await using var db = CreateContext();
        var transfer = SeedTransfer(db, out _, out _);

        var handler = new GetTransferQueryHandler(CurrentUser("carol"), db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetTransferQuery(transfer.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownTransferId_ThrowsNotFound()
    {
        await using var db = CreateContext();
        SeedTransfer(db, out var senderUsername, out _);

        var handler = new GetTransferQueryHandler(CurrentUser(senderUsername), db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetTransferQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownCurrentUser_ThrowsNotFound()
    {
        await using var db = CreateContext();
        SeedTransfer(db, out _, out _);

        var handler = new GetTransferQueryHandler(CurrentUser("ghost"), db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetTransferQuery(Guid.NewGuid()), CancellationToken.None));
    }

    private static FakeWalletDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FakeWalletDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Transfer SeedTransfer(FakeWalletDbContext db, out string senderUsername, out string recipientUsername)
    {
        senderUsername = "alice";
        recipientUsername = "bob";

        var now = new DateTime(2026, 10, 18, 9, 30, 0, DateTimeKind.Utc);
        var alice = User.Create(senderUsername, "alice@example.com", now);
        var bob = User.Create(recipientUsername, "bob@example.com", now);
        var carol = User.Create("carol", "carol@example.com", now);
        var aliceWallet = WalletAccount.Create(alice.Id, now);
        var bobWallet = WalletAccount.Create(bob.Id, now);

        db.Users.AddRange(alice, bob, carol);
        db.Wallets.AddRange(aliceWallet, bobWallet);

        var transfer = Transfer.Create(aliceWallet.Id, bobWallet.Id, 25.00m, now);
        db.Transfers.Add(transfer);
        db.SaveChanges();

        return transfer;
    }

    private static ICurrentUser CurrentUser(string username)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Username.Returns(username);
        return currentUser;
    }
}
