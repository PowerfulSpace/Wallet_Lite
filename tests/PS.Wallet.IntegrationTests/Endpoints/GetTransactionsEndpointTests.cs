using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PS.Wallet.Infrastructure.Persistence;
using Xunit;

namespace PS.Wallet.IntegrationTests.Endpoints;

public sealed class GetTransactionsEndpointTests : IClassFixture<WalletApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GetTransactionsEndpointTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTransactions_Deposit_ReturnsDepositItem()
    {
        var alice = Client("alice");
        var depositId = await FundAsync(alice, "50.00");

        var page = await GetTransactionsAsync(alice, limit: 100);

        var item = Assert.Single(page.Items, i => i.RelatedOperationId == depositId);
        Assert.Equal("deposit", item.Type);
        Assert.Equal(50.00m, item.Amount);
        Assert.Equal("USD", item.Currency);
        Assert.Equal("Virtual deposit", item.Description);
    }

    [Fact]
    public async Task GetTransactions_OutgoingTransfer_ReturnsTransferOutWithNegativeAmount()
    {
        var alice = Client("alice");
        await FundAsync(alice, "100.00");
        var transferId = await TransferAsync(alice, "bob", "30.00");

        var page = await GetTransactionsAsync(alice, limit: 100);

        var item = Assert.Single(page.Items, i => i.RelatedOperationId == transferId);
        Assert.Equal("transfer_out", item.Type);
        Assert.Equal(-30.00m, item.Amount);
        Assert.Equal("Transfer to bob", item.Description);
    }

    [Fact]
    public async Task GetTransactions_IncomingTransfer_ReturnsTransferInWithPositiveAmount()
    {
        var alice = Client("alice");
        var bob = Client("bob");
        await FundAsync(alice, "100.00");
        var transferId = await TransferAsync(alice, "bob", "30.00");

        var page = await GetTransactionsAsync(bob, limit: 100);

        var item = Assert.Single(page.Items, i => i.RelatedOperationId == transferId);
        Assert.Equal("transfer_in", item.Type);
        Assert.Equal(30.00m, item.Amount);
        Assert.Equal("Transfer from alice", item.Description);
    }

    [Fact]
    public async Task GetTransactions_ReturnsNewestFirst()
    {
        var alice = Client("alice");
        await FundAsync(alice, "10.00");
        await FundAsync(alice, "20.00");

        var page = await GetTransactionsAsync(alice, limit: 100);

        Assert.NotEmpty(page.Items);
        var timestamps = page.Items.Select(i => i.CreatedAtUtc).ToList();
        Assert.Equal(timestamps.OrderByDescending(t => t).ToList(), timestamps);
    }

    [Fact]
    public async Task GetTransactions_CursorRoundTrip_PagesThroughAllItemsWithoutDuplicates()
    {
        var alice = Client("alice");
        var createdIds = new List<Guid>();
        for (var i = 0; i < 25; i++)
        {
            createdIds.Add(await FundAsync(alice, "1.00"));
        }

        var all = new List<TransactionResponse>();
        var cursors = new HashSet<string>();
        var cursor = (string?)null;
        var pages = 0;

        while (true)
        {
            var page = await GetTransactionsAsync(alice, limit: 20, cursor: cursor);
            pages++;

            Assert.True(page.Items.Count <= 20);

            if (pages == 1)
            {
                Assert.Equal(20, page.Items.Count);
                Assert.True(page.HasMore);
                Assert.NotNull(page.NextCursor);
            }
            else if (page.HasMore)
            {
                Assert.NotNull(page.NextCursor);
            }
            else
            {
                Assert.Null(page.NextCursor);
            }

            all.AddRange(page.Items);

            if (!page.HasMore)
            {
                break;
            }

            Assert.True(cursors.Add(page.NextCursor!), "The server returned a repeated cursor.");
            cursor = page.NextCursor;
        }

        Assert.True(pages >= 2);

        var ids = all.Select(i => i.TransactionId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        foreach (var depositId in createdIds)
        {
            Assert.Single(all, i => i.RelatedOperationId == depositId);
        }

        var timestamps = all.Select(i => i.CreatedAtUtc).ToList();
        Assert.Equal(timestamps.OrderByDescending(t => t).ToList(), timestamps);
    }

    [Fact]
    public async Task GetTransactions_EqualTimestamps_PagesWithoutDuplicatesOrSkips()
    {
        var alice = Client("alice");
        var depositId = await FundAsync(alice, "5.00");

        var tiedIds = new List<Guid>();
        var tiedCreatedAtUtc = DateTime.UtcNow.AddDays(-1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
            var userId = await db.Users.Where(u => u.Username == "alice").Select(u => u.Id).SingleAsync();
            var walletId = await db.Wallets.Where(w => w.UserId == userId).Select(w => w.Id).SingleAsync();

            for (var i = 0; i < 3; i++)
            {
                var entryId = Guid.NewGuid();
                tiedIds.Add(entryId);

                await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO LedgerEntries (Id, WalletId, DepositId, TransferId, Amount, CreatedAtUtc)
VALUES ({entryId}, {walletId}, {depositId}, NULL, {1.00m + i}, {tiedCreatedAtUtc})");
            }
        }

        var all = new List<TransactionResponse>();
        var cursor = (string?)null;
        var terminated = false;

        for (var page = 0; page < 200; page++)
        {
            var result = await GetTransactionsAsync(alice, limit: 1, cursor: cursor);
            all.AddRange(result.Items);

            if (!result.HasMore)
            {
                Assert.Null(result.NextCursor);
                terminated = true;
                break;
            }

            Assert.NotNull(result.NextCursor);
            cursor = result.NextCursor;
        }

        Assert.True(terminated, "Pagination did not terminate.");
        Assert.Equal(3, tiedIds.Distinct().Count());

        foreach (var tiedId in tiedIds)
        {
            Assert.Single(all, i => i.TransactionId == tiedId);
        }

        var timestamps = all.Select(i => i.CreatedAtUtc).ToList();
        Assert.Equal(timestamps.OrderByDescending(t => t).ToList(), timestamps);
    }

    [Fact]
    public async Task GetTransactions_InvalidCursor_Returns400InvalidCursor()
    {
        var alice = Client("alice");

        var response = await alice.GetAsync("/api/v1/wallet/transactions?cursor=not-a-valid-cursor");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_CURSOR", await GetCodeAsync(response));
    }

    [Fact]
    public async Task GetTransactions_LimitAboveMaximum_Returns400ValidationError()
    {
        var alice = Client("alice");

        var response = await alice.GetAsync("/api/v1/wallet/transactions?limit=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await GetCodeAsync(response));
    }

    [Fact]
    public async Task GetTransactions_OtherUserTransactions_AreNotVisible()
    {
        var alice = Client("alice");
        var bob = Client("bob");
        var aliceDepositId = await FundAsync(alice, "50.00");
        await TransferAsync(alice, "bob", "10.00");

        var page = await GetTransactionsAsync(bob, limit: 100);

        Assert.DoesNotContain(page.Items, i => i.RelatedOperationId == aliceDepositId);
    }

    [Fact]
    public async Task GetTransactions_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/wallet/transactions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient Client(string user)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", user);
        return client;
    }

    private static async Task<Guid> FundAsync(HttpClient client, string amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/deposits")
        {
            Content = new StringContent($"{{\"amount\":\"{amount}\"}}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", $"dep-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DepositResponse>();
        Assert.NotNull(body);
        return body!.DepositId;
    }

    private static async Task<Guid> TransferAsync(HttpClient client, string recipientUsername, string amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers")
        {
            Content = new StringContent(
                $"{{\"recipientUsername\":\"{recipientUsername}\",\"amount\":\"{amount}\"}}",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add("Idempotency-Key", $"trf-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TransferResponse>();
        Assert.NotNull(body);
        return body!.TransferId;
    }

    private static async Task<PagedTransactionsResponse> GetTransactionsAsync(
        HttpClient client, int limit, string? cursor = null)
    {
        var url = $"/api/v1/wallet/transactions?limit={limit}";
        if (cursor is not null)
        {
            url += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedTransactionsResponse>();
        Assert.NotNull(page);
        return page!;
    }

    private static async Task<string?> GetCodeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private sealed record DepositResponse(Guid DepositId);

    private sealed record TransferResponse(Guid TransferId);

    private sealed record TransactionResponse(
        Guid TransactionId,
        string Type,
        decimal Amount,
        string Currency,
        string Description,
        Guid RelatedOperationId,
        DateTime CreatedAtUtc);

    private sealed record PagedTransactionsResponse(
        IReadOnlyList<TransactionResponse> Items,
        string? NextCursor,
        bool HasMore);
}

public sealed class GetTransactionsEmptyHistoryEndpointTests : IClassFixture<WalletApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GetTransactionsEmptyHistoryEndpointTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTransactions_NoTransactions_ReturnsEmptyPage()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "alice");

        var response = await client.GetAsync("/api/v1/wallet/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedTransactionsResponse>();
        Assert.NotNull(page);
        Assert.Empty(page!.Items);
        Assert.False(page.HasMore);
        Assert.Null(page.NextCursor);
    }

    private sealed record PagedTransactionsResponse(
        IReadOnlyList<JsonElement> Items,
        string? NextCursor,
        bool HasMore);
}
