using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PS.Wallet.IntegrationTests.Endpoints;

public sealed class TransferEndpointTests : IClassFixture<WalletApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TransferEndpointTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateTransfer_ValidRequest_Returns201WithLocationAndBody()
    {
        var alice = Client("alice");
        var bob = Client("bob");
        await FundAsync(alice, "100.00");

        var aliceBefore = await GetBalanceAsync(alice);
        var bobBefore = await GetBalanceAsync(bob);

        var response = await SendAsync(alice, "bob", "40.00", NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TransferResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.TransferId);
        Assert.Equal("alice", body.SenderUsername);
        Assert.Equal("bob", body.RecipientUsername);
        Assert.Equal(40.00m, body.Amount);
        Assert.Equal("USD", body.Currency);
        Assert.EndsWith($"/api/v1/transfers/{body.TransferId}", response.Headers.Location!.ToString());

        Assert.Equal(aliceBefore - 40.00m, await GetBalanceAsync(alice));
        Assert.Equal(bobBefore + 40.00m, await GetBalanceAsync(bob));
    }

    [Fact]
    public async Task CreateTransfer_ReplayedIdempotencyKey_ReturnsSameResultWithoutSecondEffect()
    {
        var alice = Client("alice");
        var bob = Client("bob");
        await FundAsync(alice, "100.00");

        var key = NewKey();
        var aliceBefore = await GetBalanceAsync(alice);
        var bobBefore = await GetBalanceAsync(bob);

        var first = await SendAsync(alice, "bob", "25.00", key);
        var second = await SendAsync(alice, "bob", "25.00", key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(first.Headers.Location!.ToString(), second.Headers.Location!.ToString());
        Assert.Equal(aliceBefore - 25.00m, await GetBalanceAsync(alice));
        Assert.Equal(bobBefore + 25.00m, await GetBalanceAsync(bob));
    }

    [Fact]
    public async Task CreateTransfer_SameKeyDifferentAmount_Returns409IdempotencyKeyReused()
    {
        var alice = Client("alice");
        await FundAsync(alice, "100.00");

        var key = NewKey();
        var first = await SendAsync(alice, "bob", "10.00", key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var reused = await SendAsync(alice, "bob", "20.00", key);

        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await GetCodeAsync(reused));
    }

    [Fact]
    public async Task CreateTransfer_InsufficientFunds_Returns409InsufficientFunds()
    {
        var alice = Client("alice");
        var balance = await GetBalanceAsync(alice);

        var tooMuch = (balance + 0.01m).ToString("0.00", CultureInfo.InvariantCulture);
        var response = await SendAsync(alice, "bob", tooMuch, NewKey());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INSUFFICIENT_FUNDS", await GetCodeAsync(response));
        Assert.Equal(balance, await GetBalanceAsync(alice));
    }

    [Fact]
    public async Task CreateTransfer_SelfTransfer_Returns409SelfTransfer()
    {
        var alice = Client("alice");

        var response = await SendAsync(alice, "alice", "5.00", NewKey());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SELF_TRANSFER", await GetCodeAsync(response));
    }

    [Fact]
    public async Task CreateTransfer_UnknownRecipient_Returns404()
    {
        var alice = Client("alice");
        await FundAsync(alice, "100.00");

        var response = await SendAsync(alice, "ghost", "10.00", NewKey());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("RESOURCE_NOT_FOUND", await GetCodeAsync(response));
    }

    [Fact]
    public async Task CreateTransfer_MissingIdempotencyKey_Returns400ValidationError()
    {
        var alice = Client("alice");

        var response = await SendAsync(alice, "bob", "10.00", key: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await GetCodeAsync(response));
    }

    [Fact]
    public async Task CreateTransfer_MalformedAmount_Returns400ValidationError()
    {
        var alice = Client("alice");

        var response = await SendAsync(alice, "bob", "not-a-number", NewKey());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await GetCodeAsync(response));
    }

    [Fact]
    public async Task CreateTransfer_InvalidRecipientUsername_Returns400ValidationError()
    {
        var alice = Client("alice");

        var response = await SendAsync(alice, "no such user!", "10.00", NewKey());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await GetCodeAsync(response));
    }

    [Fact]
    public async Task CreateTransfer_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await SendAsync(client, "bob", "10.00", NewKey());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransfer_ConcurrentFullBalanceTransfers_NeverOverdraws()
    {
        var alice = Client("alice");
        var bob = Client("bob");
        await FundAsync(alice, "100.00");

        var aliceBefore = await GetBalanceAsync(alice);
        var bobBefore = await GetBalanceAsync(bob);
        Assert.True(aliceBefore > 0m);

        var amount = aliceBefore.ToString("0.00", CultureInfo.InvariantCulture);

        var responses = await Task.WhenAll(
            SendAsync(alice, "bob", amount, NewKey()),
            SendAsync(alice, "bob", amount, NewKey()));

        var created = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
        var rejected = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();

        Assert.Single(created);
        Assert.Single(rejected);
        Assert.Equal("INSUFFICIENT_FUNDS", await GetCodeAsync(rejected[0]));

        var aliceAfter = await GetBalanceAsync(alice);
        Assert.Equal(aliceBefore - decimal.Parse(amount, CultureInfo.InvariantCulture), aliceAfter);
        Assert.Equal(bobBefore + decimal.Parse(amount, CultureInfo.InvariantCulture), await GetBalanceAsync(bob));
    }

    [Fact]
    public async Task GetTransfer_CreatedTransfer_Returns200WithSameBody()
    {
        var alice = Client("alice");
        await FundAsync(alice, "100.00");

        var created = await SendAsync(alice, "bob", "15.00", NewKey());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<TransferResponse>();
        Assert.NotNull(createdBody);

        var response = await alice.GetAsync($"/api/v1/transfers/{createdBody!.TransferId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TransferResponse>();
        Assert.Equal(createdBody, body);
    }

    [Fact]
    public async Task GetTransfer_RecipientCanRetrieve_Returns200()
    {
        var alice = Client("alice");
        var bob = Client("bob");
        await FundAsync(alice, "100.00");

        var created = await SendAsync(alice, "bob", "15.00", NewKey());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<TransferResponse>();

        var response = await bob.GetAsync($"/api/v1/transfers/{createdBody!.TransferId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTransfer_UnknownId_Returns404()
    {
        var alice = Client("alice");

        var response = await alice.GetAsync($"/api/v1/transfers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTransfer_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/transfers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient Client(string user)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", user);
        return client;
    }

    private static async Task FundAsync(HttpClient client, string amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/deposits")
        {
            Content = new StringContent($"{{\"amount\":\"{amount}\"}}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", $"dep-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static HttpRequestMessage BuildRequest(string recipientUsername, string amount, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers")
        {
            Content = new StringContent(
                $"{{\"recipientUsername\":\"{recipientUsername}\",\"amount\":\"{amount}\"}}",
                Encoding.UTF8,
                "application/json"),
        };

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return request;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, string recipientUsername, string amount, string? key)
    {
        return await client.SendAsync(BuildRequest(recipientUsername, amount, key));
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

    private static string NewKey() => $"trf-{Guid.NewGuid():N}";

    private sealed record WalletResponse(Guid WalletId, string Currency, decimal Balance, DateTime CreatedAtUtc);

    private sealed record TransferResponse(
        Guid TransferId,
        string SenderUsername,
        string RecipientUsername,
        decimal Amount,
        string Currency,
        DateTime CreatedAtUtc);
}
