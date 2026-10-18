using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PS.Wallet.IntegrationTests.Endpoints;

public sealed class GetWalletEndpointTests : IClassFixture<WalletApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GetWalletEndpointTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWallet_WithValidTestUser_Returns200()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "alice");

        var response = await client.GetAsync("/api/v1/wallet");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWallet_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/wallet");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWallet_WithUnknownUser_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "ghost");
        var response = await client.GetAsync("/api/v1/wallet");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
