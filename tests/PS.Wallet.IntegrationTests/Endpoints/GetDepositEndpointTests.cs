using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PS.Wallet.IntegrationTests.Endpoints;

public sealed class GetDepositEndpointTests : IClassFixture<WalletApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GetDepositEndpointTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDeposit_CreatedDeposit_Returns200WithSameBody()
    {
        var client = Client();

        var created = await client.SendAsync(BuildCreateRequest("50.00", NewKey()));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<DepositResponse>();
        Assert.NotNull(createdBody);

        var response = await client.GetAsync($"/api/v1/wallet/deposits/{createdBody!.DepositId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DepositResponse>();
        Assert.Equal(createdBody, body);
    }

    [Fact]
    public async Task GetDeposit_LocationFromCreateResponse_PointsToRetrievableEndpoint()
    {
        var client = Client();

        var created = await client.SendAsync(BuildCreateRequest("10.00", NewKey()));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var location = created.Headers.Location!.ToString();
        var createdBody = await created.Content.ReadFromJsonAsync<DepositResponse>();
        Assert.EndsWith($"/api/v1/wallet/deposits/{createdBody!.DepositId}", location);

        var response = await client.GetAsync(location);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDeposit_UnknownId_Returns404()
    {
        var client = Client();

        var response = await client.GetAsync($"/api/v1/wallet/deposits/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDeposit_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/wallet/deposits/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient Client()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "alice");
        return client;
    }

    private static HttpRequestMessage BuildCreateRequest(string amount, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/deposits")
        {
            Content = new StringContent($"{{\"amount\":\"{amount}\"}}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static string NewKey() => $"dep-{Guid.NewGuid():N}";

    private sealed record DepositResponse(
        Guid DepositId,
        Guid WalletId,
        decimal Amount,
        string Currency,
        decimal BalanceAfter,
        DateTime CreatedAtUtc);
}
