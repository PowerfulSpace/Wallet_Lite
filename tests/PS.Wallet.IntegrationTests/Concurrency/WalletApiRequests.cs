using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PS.Wallet.IntegrationTests.Endpoints;
using PS.Wallet.Infrastructure.Persistence;
using Xunit;

namespace PS.Wallet.IntegrationTests.Concurrency;

internal static class WalletApiRequests
{
    internal static HttpClient ClientFor(WalletApiFactory factory, string username)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", username);
        return client;
    }

    /// <summary>
    /// Releases every sender at the same instant so the requests overlap, without relying on timing delays.
    /// </summary>
    internal static async Task<HttpResponseMessage[]> SendTogetherAsync(params Func<Task<HttpResponseMessage>>[] senders)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = senders.Select(sender => SendWhenReleasedAsync(sender, gate.Task)).ToArray();

        gate.SetResult();

        return await Task.WhenAll(pending);
    }

    internal static async Task<HttpResponseMessage> SendDepositAsync(HttpClient client, string amount, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/deposits")
        {
            Content = new StringContent($"{{\"amount\":\"{amount}\"}}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", key);

        return await client.SendAsync(request);
    }

    internal static async Task<HttpResponseMessage> SendTransferAsync(
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

    internal static async Task<decimal> FundAsync(HttpClient client, string amount)
    {
        var response = await SendDepositAsync(client, amount, NewKey("fund"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await GetBalanceAsync(client);
    }

    internal static async Task<decimal> GetBalanceAsync(HttpClient client)
    {
        var wallet = await client.GetFromJsonAsync<WalletResponse>("/api/v1/wallet");
        Assert.NotNull(wallet);
        return wallet!.Balance;
    }

    internal static async Task<string?> GetErrorCodeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    internal static Task<int> CountTransfersAsync(WalletApiFactory factory) =>
        WithDbAsync(factory, db => db.Transfers.CountAsync());

    internal static Task<int> CountTransferLedgerEntriesAsync(WalletApiFactory factory) =>
        WithDbAsync(factory, db => db.LedgerEntries.CountAsync(e => e.TransferId != null));

    internal static Task<int> CountDepositsAsync(WalletApiFactory factory, decimal amount) =>
        WithDbAsync(factory, db => db.Deposits.CountAsync(d => d.Amount == amount));

    internal static Task<int> CountIdempotencyRecordsAsync(WalletApiFactory factory, string key) =>
        WithDbAsync(factory, db => db.IdempotencyRecords.CountAsync(r => r.IdempotencyKey == key));

    internal static Task<decimal> WalletLedgerSumAsync(WalletApiFactory factory, string username) =>
        WithDbAsync(factory, async db =>
        {
            var walletId = await db.Wallets
                .Where(w => db.Users.Any(u => u.Id == w.UserId && u.Username == username))
                .Select(w => w.Id)
                .SingleAsync();

            return await db.LedgerEntries.Where(e => e.WalletId == walletId).SumAsync(e => e.Amount);
        });

    internal static async Task<T> WithDbAsync<T>(WalletApiFactory factory, Func<WalletDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        return await action(db);
    }

    internal static string NewKey(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<HttpResponseMessage> SendWhenReleasedAsync(Func<Task<HttpResponseMessage>> sender, Task gate)
    {
        await gate;
        return await sender();
    }

    internal sealed record WalletResponse(Guid WalletId, string Currency, decimal Balance, DateTime CreatedAtUtc);
}
