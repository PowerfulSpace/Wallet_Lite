using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using PS.Wallet.Application;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.DTOs;
using PS.Wallet.Application.Wallets.Queries.GetTransactions;
using PS.Wallet.Domain.Entities;
using PS.Wallet.UnitTests.TestSupport;
using Xunit;

namespace PS.Wallet.UnitTests.Application;

public sealed class GetTransactionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_EmptyHistory_ReturnsEmptyPage()
    {
        await using var db = CreateContext();
        SeedUserWithWallet(db, "alice");

        var handler = new GetTransactionsQueryHandler(CurrentUser("alice"), db);

        var result = await handler.Handle(new GetTransactionsQuery(null, 20), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task Handle_SingleDeposit_ReturnsDepositItem()
    {
        await using var db = CreateContext();
        var wallet = SeedUserWithWallet(db, "alice");
        var entry = AddDeposit(db, wallet.Id, 50.00m, BaseTime);

        var handler = new GetTransactionsQueryHandler(CurrentUser("alice"), db);

        var result = await handler.Handle(new GetTransactionsQuery(null, 20), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(entry.Id, item.TransactionId);
        Assert.Equal("deposit", item.Type);
        Assert.Equal(50.00m, item.Amount);
        Assert.Equal("USD", item.Currency);
        Assert.Equal(entry.DepositId, item.RelatedOperationId);
        Assert.Equal(BaseTime, item.CreatedAtUtc);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task Handle_IncomingTransfer_ReturnsPositiveTransferIn()
    {
        await using var db = CreateContext();
        var aliceWallet = SeedUserWithWallet(db, "alice");
        var bobWallet = SeedUserWithWallet(db, "bob");
        var entry = AddTransfer(db, aliceWallet.Id, bobWallet.Id, 25.00m, BaseTime, forSender: false);

        var handler = new GetTransactionsQueryHandler(CurrentUser("bob"), db);

        var result = await handler.Handle(new GetTransactionsQuery(null, 20), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(entry.Id, item.TransactionId);
        Assert.Equal("transfer_in", item.Type);
        Assert.Equal(25.00m, item.Amount);
        Assert.Equal(entry.TransferId, item.RelatedOperationId);
        Assert.Equal("Transfer from alice", item.Description);
    }

    [Fact]
    public async Task Handle_OutgoingTransfer_ReturnsNegativeTransferOut()
    {
        await using var db = CreateContext();
        var aliceWallet = SeedUserWithWallet(db, "alice");
        var bobWallet = SeedUserWithWallet(db, "bob");
        var entry = AddTransfer(db, aliceWallet.Id, bobWallet.Id, 25.00m, BaseTime, forSender: true);

        var handler = new GetTransactionsQueryHandler(CurrentUser("alice"), db);

        var result = await handler.Handle(new GetTransactionsQuery(null, 20), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(entry.Id, item.TransactionId);
        Assert.Equal("transfer_out", item.Type);
        Assert.Equal(-25.00m, item.Amount);
        Assert.Equal(entry.TransferId, item.RelatedOperationId);
        Assert.Equal("Transfer to bob", item.Description);
    }

    [Fact]
    public async Task Handle_OrderedNewestFirst()
    {
        await using var db = CreateContext();
        var wallet = SeedUserWithWallet(db, "alice");
        AddDeposit(db, wallet.Id, 10.00m, BaseTime);
        AddDeposit(db, wallet.Id, 20.00m, BaseTime.AddSeconds(1));
        AddDeposit(db, wallet.Id, 30.00m, BaseTime.AddSeconds(2));

        var handler = new GetTransactionsQueryHandler(CurrentUser("alice"), db);

        var result = await handler.Handle(new GetTransactionsQuery(null, 20), CancellationToken.None);

        Assert.Equal(new[] { 30.00m, 20.00m, 10.00m }, result.Items.Select(i => i.Amount));
    }

    [Fact]
    public async Task Handle_MoreItemsThanLimit_ReturnsNextCursorAndNextPage()
    {
        await using var db = CreateContext();
        var wallet = SeedUserWithWallet(db, "alice");

        var expectedIds = new List<Guid>();
        for (var i = 0; i < 25; i++)
        {
            expectedIds.Add(AddDeposit(db, wallet.Id, 1.00m + i, BaseTime.AddSeconds(i)).Id);
        }

        var handler = new GetTransactionsQueryHandler(CurrentUser("alice"), db);

        var firstPage = await handler.Handle(new GetTransactionsQuery(null, 20), CancellationToken.None);

        Assert.Equal(20, firstPage.Items.Count);
        Assert.True(firstPage.HasMore);
        Assert.NotNull(firstPage.NextCursor);
        Assert.Equal(expectedIds[24], firstPage.Items[0].TransactionId);
        Assert.Equal(expectedIds[5], firstPage.Items[^1].TransactionId);

        var secondPage = await handler.Handle(
            new GetTransactionsQuery(firstPage.NextCursor, 20), CancellationToken.None);

        Assert.Equal(5, secondPage.Items.Count);
        Assert.False(secondPage.HasMore);
        Assert.Null(secondPage.NextCursor);

        var allIds = firstPage.Items.Concat(secondPage.Items).Select(i => i.TransactionId).ToList();
        Assert.Equal(25, allIds.Count);
        Assert.Equal(25, allIds.Distinct().Count());
        Assert.Equal(expectedIds.ToHashSet(), allIds.ToHashSet());
    }

    [Fact]
    public async Task Handle_EqualsTimestamp_OrdersByIdDescending()
    {
        await using var db = CreateContext();
        var wallet = SeedUserWithWallet(db, "alice");

        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add(AddDeposit(db, wallet.Id, 5.00m, BaseTime).Id);
        }

        var handler = new GetTransactionsQueryHandler(CurrentUser("alice"), db);

        var result = await handler.Handle(new GetTransactionsQuery(null, 20), CancellationToken.None);

        var expected = ids.OrderByDescending(id => id).ToList();
        Assert.Equal(expected, result.Items.Select(i => i.TransactionId).ToList());
    }

    [Fact]
    public async Task Handle_InvalidCursor_ThrowsInvalidCursor()
    {
        await using var db = CreateContext();
        SeedUserWithWallet(db, "alice");

        var handler = new GetTransactionsQueryHandler(CurrentUser("alice"), db);

        await Assert.ThrowsAsync<InvalidCursorException>(() =>
            handler.Handle(new GetTransactionsQuery("not-a-valid-cursor", 20), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        await using var db = CreateContext();

        var handler = new GetTransactionsQueryHandler(CurrentUser("ghost"), db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetTransactionsQuery(null, 20), CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validator_LimitOutOfRange_IsRejected(int limit)
    {
        var validator = new GetTransactionsQueryValidator();

        var result = validator.Validate(new GetTransactionsQuery(null, limit));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    public void Validator_LimitWithinRange_IsAccepted(int limit)
    {
        var validator = new GetTransactionsQueryValidator();

        var result = validator.Validate(new GetTransactionsQuery(null, limit));

        Assert.True(result.IsValid);
    }

    private static readonly DateTime BaseTime = new(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);

    private static FakeWalletDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FakeWalletDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static WalletAccount SeedUserWithWallet(FakeWalletDbContext db, string username)
    {
        var now = DateTime.UtcNow;
        var user = User.Create(username, $"{username}@example.com", now);
        var wallet = WalletAccount.Create(user.Id, now);
        db.Users.Add(user);
        db.Wallets.Add(wallet);
        db.SaveChanges();
        return wallet;
    }

    private static LedgerEntry AddDeposit(FakeWalletDbContext db, Guid walletId, decimal amount, DateTime createdAtUtc)
    {
        var deposit = Deposit.Create(walletId, amount, createdAtUtc);
        var entry = LedgerEntry.ForDeposit(walletId, deposit.Id, amount, createdAtUtc);
        db.Deposits.Add(deposit);
        db.LedgerEntries.Add(entry);
        db.SaveChanges();
        return entry;
    }

    private static LedgerEntry AddTransfer(
        FakeWalletDbContext db,
        Guid senderWalletId,
        Guid recipientWalletId,
        decimal amount,
        DateTime createdAtUtc,
        bool forSender)
    {
        var transfer = Transfer.Create(senderWalletId, recipientWalletId, amount, createdAtUtc);
        var entry = forSender
            ? LedgerEntry.ForTransfer(senderWalletId, transfer.Id, -amount, createdAtUtc)
            : LedgerEntry.ForTransfer(recipientWalletId, transfer.Id, amount, createdAtUtc);

        db.Transfers.Add(transfer);
        db.LedgerEntries.Add(entry);
        db.SaveChanges();
        return entry;
    }

    private static ICurrentUser CurrentUser(string username)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Username.Returns(username);
        return currentUser;
    }
}
