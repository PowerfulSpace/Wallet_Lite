using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PS.Wallet.IntegrationTests.Endpoints;

public sealed class CreateDepositEndpointTests : IClassFixture<WalletApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CreateDepositEndpointTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateDeposit_ValidRequest_Returns201WithLocationAndBody()
    {
        var client = Client();

        var response = await SendAsync(client, "50.00", NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DepositResponse>();
        Assert.NotNull(body);
        Assert.Equal(50.00m, body!.Amount);
        Assert.Equal("USD", body.Currency);
        Assert.NotEqual(Guid.Empty, body.DepositId);
        Assert.NotEqual(Guid.Empty, body.WalletId);
        Assert.EndsWith($"/api/v1/wallet/deposits/{body.DepositId}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task CreateDeposit_ReplayedIdempotencyKey_ReturnsSameResultWithoutSecondEffect()
    {
        var client = Client();
        var before = await GetBalanceAsync(client);
        var key = NewKey();

        var first = await SendAsync(client, "10.00", key);
        var second = await SendAsync(client, "10.00", key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(first.Headers.Location!.ToString(), second.Headers.Location!.ToString());
        Assert.Equal(before + 10.00m, await GetBalanceAsync(client));
    }

    [Fact]
    public async Task CreateDeposit_SameKeyDifferentAmount_Returns409IdempotencyKeyReused()
    {
        var client = Client();
        var key = NewKey();

        await SendAsync(client, "10.00", key);
        var reused = await SendAsync(client, "20.00", key);

        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await GetCodeAsync(reused));
    }

    [Fact]
    public async Task CreateDeposit_ExceedingBalanceLimit_Returns409BalanceLimitExceeded()
    {
        var client = Client("bob");
        var balance = await GetBalanceAsync(client);
        Assert.True(balance < 1_000_000.00m);

        var headroom = (1_000_000.00m - balance).ToString("0.00", CultureInfo.InvariantCulture);
        var fill = await SendAsync(client, headroom, NewKey());
        Assert.Equal(HttpStatusCode.Created, fill.StatusCode);

        var exceeding = await SendAsync(client, "0.01", NewKey());

        Assert.Equal(HttpStatusCode.Conflict, exceeding.StatusCode);
        Assert.Equal("BALANCE_LIMIT_EXCEEDED", await GetCodeAsync(exceeding));
    }

    [Fact]
    public async Task CreateDeposit_MissingIdempotencyKey_Returns400ValidationError()
    {
        var client = Client();

        var response = await SendAsync(client, "50.00", key: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await GetCodeAsync(response));
    }

    [Fact]
    public async Task CreateDeposit_MalformedIdempotencyKey_Returns400ValidationError()
    {
        var client = Client();

        var response = await SendAsync(client, "50.00", "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await GetCodeAsync(response));
    }

    [Fact]
    public async Task CreateDeposit_ConcurrentSameKey_AppliesSingleEffect()
    {
        var client = Client();
        var before = await GetBalanceAsync(client);
        var key = NewKey();

        var responses = await Task.WhenAll(
            SendAsync(client, "25.00", key),
            SendAsync(client, "25.00", key));

        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.ServiceUnavailable,
            $"Unexpected status code {r.StatusCode}."));

        var created = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
        Assert.NotEmpty(created);

        var bodies = new List<string>();
        foreach (var response in created)
        {
            bodies.Add(await response.Content.ReadAsStringAsync());
        }

        Assert.Single(bodies.Distinct());
        Assert.Equal(before + 25.00m, await GetBalanceAsync(client));
    }

    private HttpClient Client(string user = "alice")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", user);
        return client;
    }

    private static HttpRequestMessage BuildRequest(string amount, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/deposits")
        {
            Content = new StringContent($"{{\"amount\":\"{amount}\"}}", Encoding.UTF8, "application/json"),
        };

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return request;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string amount, string? key)
    {
        return await client.SendAsync(BuildRequest(amount, key));
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

    private static string NewKey() => $"dep-{Guid.NewGuid():N}";

    private sealed record WalletResponse(Guid WalletId, string Currency, decimal Balance, DateTime CreatedAtUtc);

    private sealed record DepositResponse(
        Guid DepositId,
        Guid WalletId,
        decimal Amount,
        string Currency,
        decimal BalanceAfter,
        DateTime CreatedAtUtc);
}
