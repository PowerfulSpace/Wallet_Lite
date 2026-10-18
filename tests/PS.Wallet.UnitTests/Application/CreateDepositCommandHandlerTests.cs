using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using PS.Wallet.Application;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Application.Wallets.Commands.CreateDeposit;
using PS.Wallet.Application.Wallets.DTOs;
using PS.Wallet.Domain.Entities;
using PS.Wallet.Infrastructure.Persistence;
using PS.Wallet.UnitTests.TestSupport;
using Xunit;

namespace PS.Wallet.UnitTests.Application;

public sealed class CreateDepositCommandHandlerTests
{
    private const string ValidKey = "deposit-key-0001";

    [Fact]
    public async Task Handle_ValidDeposit_ReservesKeyExecutesAndCompletes()
    {
        await using var db = CreateContext();
        var user = SeedUser(db, "alice");

        var expected = new DepositDto(Guid.NewGuid(), Guid.NewGuid(), 50.00m, "USD", 50.00m, DateTime.UtcNow);

        var executor = Substitute.For<IDepositTransactionExecutor>();
        executor.ExecuteAsync(user.Id, 50.00m, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var reservationId = Guid.NewGuid();
        var reservation = new IdempotencyLookupResult(IdempotencyLookupStatus.Reserved, reservationId);
        var coordinator = Reserve(ValidKey, reservation);

        var handler = new CreateDepositCommandHandler(CurrentUser("alice"), db, executor, coordinator);

        var result = await handler.Handle(new CreateDepositCommand(50.00m, ValidKey), CancellationToken.None);

        Assert.Equal(expected, result);

        await coordinator.Received(1).LookupOrReserveAsync(
            user.Id, "deposit", ValidKey, Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await executor.Received(1).ExecuteAsync(user.Id, 50.00m, reservationId, Arg.Any<CancellationToken>());
        await coordinator.Received(1).CompleteAsync(
            reservationId,
            expected.DepositId,
            201,
            Arg.Is<string>(payload => JsonSerializer.Deserialize<DepositDto>(payload) == expected),
            Arg.Any<CancellationToken>());
        await coordinator.DidNotReceiveWithAnyArgs().ReleaseAsync(default, default);
    }

    [Fact]
    public async Task Handle_KeyScopeAndFingerprintAreScopedToTheCurrentUserAndAmount()
    {
        await using var db = CreateContext();
        var user = SeedUser(db, "alice");

        var executor = Substitute.For<IDepositTransactionExecutor>();
        executor.ExecuteAsync(user.Id, Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new DepositDto(Guid.NewGuid(), Guid.NewGuid(), 50.00m, "USD", 50.00m, DateTime.UtcNow));

        var coordinator = Reserve(ValidKey, new IdempotencyLookupResult(IdempotencyLookupStatus.Reserved, Guid.NewGuid()));

        var handler = new CreateDepositCommandHandler(CurrentUser("alice"), db, executor, coordinator);

        await handler.Handle(new CreateDepositCommand(50.00m, ValidKey), CancellationToken.None);

        await coordinator.Received(1).LookupOrReserveAsync(
            user.Id,
            "deposit",
            ValidKey,
            Arg.Is<byte[]>(fingerprint => fingerprint.SequenceEqual(Fingerprint(50.00m))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingKeyWithSameFingerprint_ReplaysStoredResult()
    {
        await using var db = CreateContext();
        SeedUser(db, "alice");

        var stored = new DepositDto(
            Guid.NewGuid(), Guid.NewGuid(), 50.00m, "USD", 50.00m,
            new DateTime(2026, 10, 18, 10, 15, 30, DateTimeKind.Utc));

        var executor = Substitute.For<IDepositTransactionExecutor>();

        var coordinator = Substitute.For<IIdempotencyCoordinator>();
        coordinator.LookupOrReserveAsync(
                Arg.Any<Guid>(), "deposit", ValidKey, Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyLookupResult(
                IdempotencyLookupStatus.Replay, Guid.NewGuid(), 201, JsonSerializer.Serialize(stored)));

        var handler = new CreateDepositCommandHandler(CurrentUser("alice"), db, executor, coordinator);

        var result = await handler.Handle(new CreateDepositCommand(50.00m, ValidKey), CancellationToken.None);

        Assert.Equal(stored, result);
        await executor.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default, default, default);
        await coordinator.DidNotReceiveWithAnyArgs().CompleteAsync(default, default, default, default!, default);
        await coordinator.DidNotReceiveWithAnyArgs().ReleaseAsync(default, default);
    }

    [Fact]
    public async Task Handle_ExistingKeyWithDifferentFingerprint_ThrowsIdempotencyKeyReused()
    {
        await using var db = CreateContext();
        SeedUser(db, "alice");

        var executor = Substitute.For<IDepositTransactionExecutor>();

        var coordinator = Substitute.For<IIdempotencyCoordinator>();
        coordinator.LookupOrReserveAsync(
                Arg.Any<Guid>(), "deposit", ValidKey, Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyLookupResult(IdempotencyLookupStatus.Conflict));

        var handler = new CreateDepositCommandHandler(CurrentUser("alice"), db, executor, coordinator);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CreateDepositCommand(50.00m, ValidKey), CancellationToken.None));

        Assert.Equal("IDEMPOTENCY_KEY_REUSED", ex.Code);
        await executor.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default, default, default);
    }

    [Fact]
    public async Task Handle_OperationStillInProgress_ThrowsIdempotencyInProgress()
    {
        await using var db = CreateContext();
        SeedUser(db, "alice");

        var executor = Substitute.For<IDepositTransactionExecutor>();

        var coordinator = Substitute.For<IIdempotencyCoordinator>();
        coordinator.LookupOrReserveAsync(
                Arg.Any<Guid>(), "deposit", ValidKey, Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyLookupResult(IdempotencyLookupStatus.InProgress, Guid.NewGuid()));

        var handler = new CreateDepositCommandHandler(CurrentUser("alice"), db, executor, coordinator);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CreateDepositCommand(50.00m, ValidKey), CancellationToken.None));

        Assert.Equal("IDEMPOTENCY_IN_PROGRESS", ex.Code);
        await executor.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default, default, default);
        await coordinator.DidNotReceiveWithAnyArgs().ReleaseAsync(default, default);
    }

    [Fact]
    public async Task Handle_BusinessRejection_ReleasesReservedKey()
    {
        await using var db = CreateContext();
        var user = SeedUser(db, "alice");

        var executor = Substitute.For<IDepositTransactionExecutor>();
        executor.ExecuteAsync(user.Id, Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<DepositDto>>(_ => throw new ConflictException("BALANCE_LIMIT_EXCEEDED", "limit"));

        var reservationId = Guid.NewGuid();
        var reservation = new IdempotencyLookupResult(IdempotencyLookupStatus.Reserved, reservationId);
        var coordinator = Reserve(ValidKey, reservation);

        var handler = new CreateDepositCommandHandler(CurrentUser("alice"), db, executor, coordinator);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CreateDepositCommand(50.00m, ValidKey), CancellationToken.None));

        await coordinator.Received(1).ReleaseAsync(reservationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UncertainCommit_DoesNotReleaseReservedKey()
    {
        await using var db = CreateContext();
        var user = SeedUser(db, "alice");

        var executor = Substitute.For<IDepositTransactionExecutor>();
        executor.ExecuteAsync(user.Id, Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<DepositDto>>(_ => throw new UncertainCommitException("unknown outcome"));

        var reservationId = Guid.NewGuid();
        var reservation = new IdempotencyLookupResult(IdempotencyLookupStatus.Reserved, reservationId);
        var coordinator = Reserve(ValidKey, reservation);

        var handler = new CreateDepositCommandHandler(CurrentUser("alice"), db, executor, coordinator);

        await Assert.ThrowsAsync<UncertainCommitException>(() =>
            handler.Handle(new CreateDepositCommand(50.00m, ValidKey), CancellationToken.None));

        await coordinator.DidNotReceiveWithAnyArgs().ReleaseAsync(default, default);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        await using var db = CreateContext();
        var executor = Substitute.For<IDepositTransactionExecutor>();
        var coordinator = Substitute.For<IIdempotencyCoordinator>();

        var handler = new CreateDepositCommandHandler(CurrentUser("ghost"), db, executor, coordinator);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CreateDepositCommand(50.00m, ValidKey), CancellationToken.None));

        await coordinator.DidNotReceiveWithAnyArgs()
            .LookupOrReserveAsync(default, default!, default!, default!, default);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5.00")]
    [InlineData("1.001")]
    [InlineData("1000000.01")]
    [InlineData("1500000.00")]
    public void Validator_InvalidAmount_IsRejected(string rawAmount)
    {
        var validator = new CreateDepositCommandValidator();

        var result = validator.Validate(new CreateDepositCommand(
            decimal.Parse(rawAmount, CultureInfo.InvariantCulture), ValidKey));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("has space")]
    [InlineData("invalid!characters")]
    public void Validator_InvalidIdempotencyKey_IsRejected(string key)
    {
        var validator = new CreateDepositCommandValidator();

        var result = validator.Validate(new CreateDepositCommand(50.00m, key));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_ValidCommand_IsAccepted()
    {
        var validator = new CreateDepositCommandValidator();

        var result = validator.Validate(new CreateDepositCommand(50.00m, ValidKey));

        Assert.True(result.IsValid);
    }

    private static IIdempotencyCoordinator Reserve(string key, IdempotencyLookupResult reservation)
    {
        var coordinator = Substitute.For<IIdempotencyCoordinator>();
        coordinator.LookupOrReserveAsync(
                Arg.Any<Guid>(), "deposit", key, Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(reservation);
        return coordinator;
    }

    private static FakeWalletDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FakeWalletDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User SeedUser(FakeWalletDbContext db, string username)
    {
        var user = User.Create(username, $"{username}@example.com", DateTime.UtcNow);
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static ICurrentUser CurrentUser(string username)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Username.Returns(username);
        return currentUser;
    }

    private static byte[] Fingerprint(decimal amount)
    {
        var canonicalAmount = amount.ToString("0.00", CultureInfo.InvariantCulture);
        return SHA256.HashData(Encoding.UTF8.GetBytes($"deposit\n-\n{canonicalAmount}"));
    }
}
