using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PS.Wallet.IntegrationTests.Endpoints;

public sealed class MoneySerializationTests : IClassFixture<WalletApiFactory>
{
    private readonly WalletApiFactory _factory;

    public MoneySerializationTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWallet_ReturnsBalanceAsString()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "alice");

        var response = await client.GetAsync("/api/v1/wallet");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var balance = doc.RootElement.GetProperty("balance");

        Assert.Equal(JsonValueKind.String, balance.ValueKind);
        Assert.Matches(@"^-?\d+\.\d{2}$", balance.GetString());
    }

    [Fact]
    public async Task CreateDeposit_WithStringAmount_ReturnsStringAmount()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "alice");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var response = await client.PostAsJsonAsync("/api/v1/wallet/deposits",
            new { amount = "50.00" });
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("amount").ValueKind);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("balanceAfter").ValueKind);
    }

    [Fact]
    public async Task CreateDeposit_WithNumberAmount_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "alice");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var response = await client.PostAsJsonAsync("/api/v1/wallet/deposits",
            new { amount = 50.00m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
