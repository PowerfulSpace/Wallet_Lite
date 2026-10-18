using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PS.Wallet.Infrastructure.Persistence;
using Xunit;

namespace PS.Wallet.IntegrationTests.Endpoints;

public sealed class IdempotencyRecoveryTests : IClassFixture<WalletApiFactory>
{
    private const string FailLedgerInsertTrigger = "wallet_test_fail_ledger_insert";

    private readonly WalletApiFactory _factory;

    public IdempotencyRecoveryTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentRequestsWithSameKey_ApplyExactlyOneFinancialEffect()
    {
        var alice = Client("alice");
        var bob = Client("bob");
        await FundAsync(alice, "100.00");

        var key = NewKey();
        var aliceBefore = await GetBalanceAsync(alice);
        var bobBefore = await GetBalanceAsync(bob);

        var responses = await Task.WhenAll(
            SendTransferAsync(alice, "bob", "21.00", key),
            SendTransferAsync(alice, "bob", "21.00", key));

        foreach (var response in responses)
        {
            Assert.True(
                response.StatusCode is HttpStatusCode.Created or HttpStatusCode.ServiceUnavailable,
                $"Unexpected status: {response.StatusCode}");
        }

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);

        Assert.Equal(aliceBefore - 21.00m, await GetBalanceAsync(alice));
        Assert.Equal(bobBefore + 21.00m, await GetBalanceAsync(bob));

        Assert.Equal(1, await WithDbAsync(db => db.IdempotencyRecords.CountAsync(r => r.IdempotencyKey == key)));
    }

    [Fact]
    public async Task ExpiredLease_InProgressRecord_IsRecoveredByRetryAndAppliesOneEffect()
    {
        var alice = Client("alice");
        var key = NewKey();

        await InsertInProgressRecordAsync(
            key, "deposit", DepositFingerprint(31.00m), DateTime.UtcNow.AddMinutes(-5));

        var aliceBefore = await GetBalanceAsync(alice);

        var recovered = await SendDepositAsync(alice, "31.00", key);

        Assert.Equal(HttpStatusCode.Created, recovered.StatusCode);
        Assert.Equal(aliceBefore + 31.00m, await GetBalanceAsync(alice));
        Assert.Equal(1, await CountDepositsAsync(31.00m));

        Assert.Equal(201, await GetRecordStatusAsync(key, "deposit"));
        Assert.NotNull(await GetRecordPayloadAsync(key, "deposit"));

        var replay = await SendDepositAsync(alice, "31.00", key);

        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await recovered.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(aliceBefore + 31.00m, await GetBalanceAsync(alice));
    }

    [Fact]
    public async Task InProgressRecordWithDifferentFingerprint_IsRejectedWithoutWaiting()
    {
        var alice = Client("alice");
        var key = NewKey();

        await InsertInProgressRecordAsync(
            key, "deposit", DepositFingerprint(11.00m), DateTime.UtcNow.AddMinutes(5));

        var aliceBefore = await GetBalanceAsync(alice);

        var response = await SendDepositAsync(alice, "12.00", key);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await GetCodeAsync(response));
        Assert.Equal(aliceBefore, await GetBalanceAsync(alice));
    }

    [Fact]
    public async Task InProgressRecordWithLiveLease_Returns503AfterBoundedWait()
    {
        var alice = Client("alice");
        var key = NewKey();

        await InsertInProgressRecordAsync(
            key, "deposit", DepositFingerprint(13.00m), DateTime.UtcNow.AddMinutes(5));

        var aliceBefore = await GetBalanceAsync(alice);

        var response = await SendDepositAsync(alice, "13.00", key);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("IDEMPOTENCY_IN_PROGRESS", await GetCodeAsync(response));
        Assert.Equal(aliceBefore, await GetBalanceAsync(alice));
    }

    [Fact]
    public async Task UncertainCommit_Returns503AndLeavesRecordInProgress_ThenRecoversOnRetry()
    {
        var alice = Client("alice");
        var key = NewKey();
        await FundAsync(alice, "50.00");

        var aliceBefore = await GetBalanceAsync(alice);
        var ledgerEntriesBefore = await CountLedgerEntriesAsync();

        await CreateFailLedgerInsertTriggerAsync();

        HttpResponseMessage? failed = null;

        try
        {
            failed = await SendDepositAsync(alice, "41.00", key);
        }
        finally
        {
            await DropFailLedgerInsertTriggerAsync();
        }

        Assert.NotNull(failed);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed!.StatusCode);
        Assert.Equal("IDEMPOTENCY_IN_PROGRESS", await GetCodeAsync(failed));
        Assert.Equal(aliceBefore, await GetBalanceAsync(alice));
        Assert.Equal(ledgerEntriesBefore, await CountLedgerEntriesAsync());
        Assert.Null(await GetRecordStatusAsync(key, "deposit"));

        await ExpireLeaseAsync(key, "deposit");

        var recovered = await SendDepositAsync(alice, "41.00", key);

        Assert.Equal(HttpStatusCode.Created, recovered.StatusCode);
        Assert.Equal(aliceBefore + 41.00m, await GetBalanceAsync(alice));
        Assert.Equal(ledgerEntriesBefore + 1, await CountLedgerEntriesAsync());
        Assert.Equal(201, await GetRecordStatusAsync(key, "deposit"));
    }

    private HttpClient Client(string user)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", user);
        return client;
    }

    private async Task<T> WithDbAsync<T>(Func<WalletDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        return await action(db);
    }

    private Task WithDbAsync(Func<WalletDbContext, Task> action) =>
        WithDbAsync(async db =>
        {
            await action(db);
            return true;
        });

    private Task InsertInProgressRecordAsync(string key, string operationType, byte[] fingerprint, DateTime leaseExpiresAtUtc) =>
        WithDbAsync(async db =>
        {
            var userId = await db.Users.Where(u => u.Username == "alice").Select(u => u.Id).SingleAsync();

            await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO IdempotencyRecords
    (Id, UserId, OperationType, IdempotencyKey, RequestFingerprint, OperationId, ResponseStatusCode, ResponsePayload, CreatedAtUtc, LeaseExpiresAtUtc)
VALUES
    ({Guid.NewGuid()}, {userId}, {operationType}, {key}, {fingerprint}, NULL, NULL, NULL, {DateTime.UtcNow}, {leaseExpiresAtUtc})");
        });

    private Task ExpireLeaseAsync(string key, string operationType) =>
        WithDbAsync(db => db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE IdempotencyRecords
SET LeaseExpiresAtUtc = DATEADD(MINUTE, -5, SYSUTCDATETIME())
WHERE IdempotencyKey = {key}
  AND OperationType = {operationType}"));

    private Task CreateFailLedgerInsertTriggerAsync() =>
        WithDbAsync(db => db.Database.ExecuteSqlRawAsync($@"
CREATE TRIGGER {FailLedgerInsertTrigger} ON LedgerEntries INSTEAD OF INSERT AS
BEGIN
    THROW 51000, 'simulated commit failure', 1;
END"));

    private Task DropFailLedgerInsertTriggerAsync() =>
        WithDbAsync(db => db.Database.ExecuteSqlRawAsync($@"
IF OBJECT_ID('{FailLedgerInsertTrigger}') IS NOT NULL
    DROP TRIGGER {FailLedgerInsertTrigger}"));

    private Task<int?> GetRecordStatusAsync(string key, string operationType) =>
        WithDbAsync(db => db.IdempotencyRecords
            .Where(r => r.IdempotencyKey == key && r.OperationType == operationType)
            .Select(r => r.ResponseStatusCode)
            .SingleOrDefaultAsync());

    private Task<string?> GetRecordPayloadAsync(string key, string operationType) =>
        WithDbAsync(db => db.IdempotencyRecords
            .Where(r => r.IdempotencyKey == key && r.OperationType == operationType)
            .Select(r => r.ResponsePayload)
            .SingleOrDefaultAsync());

    private Task<int> CountDepositsAsync(decimal amount) =>
        WithDbAsync(db => db.Deposits.CountAsync(d => d.Amount == amount));

    private Task<int> CountLedgerEntriesAsync() =>
        WithDbAsync(db => db.LedgerEntries.CountAsync());

    private static async Task FundAsync(HttpClient client, string amount)
    {
        var response = await SendDepositAsync(client, amount, $"dep-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendDepositAsync(HttpClient client, string amount, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/deposits")
        {
            Content = new StringContent($"{{\"amount\":\"{amount}\"}}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", key);

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendTransferAsync(
        HttpClient client, string recipientUsername, string amount, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers")
        {
            Content = new StringContent(
                $"{{\"recipientUsername\":\"{recipientUsername}\",\"amount\":\"{amount}\"}}",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add("Idempotency-Key", key);

        return await client.SendAsync(request);
    }

    private static async Task<decimal> GetBalanceAsync(HttpClient client)
    {
        var wallet = await client.GetFromJsonAsync<WalletResponse>("/api/v1/wallet");
        Assert.NotNull(wallet);
        return wallet!.Balance;
    }

    private static async Task<string?> GetCodeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static byte[] DepositFingerprint(decimal amount)
    {
        var canonicalAmount = amount.ToString("0.00", CultureInfo.InvariantCulture);
        return SHA256.HashData(Encoding.UTF8.GetBytes($"deposit\n-\n{canonicalAmount}"));
    }

    private static string NewKey() => $"recovery-{Guid.NewGuid():N}";

    private sealed record WalletResponse(Guid WalletId, string Currency, decimal Balance, DateTime CreatedAtUtc);
}
