using System.Net;
using PS.Wallet.IntegrationTests.Endpoints;
using Xunit;

namespace PS.Wallet.IntegrationTests.Concurrency;

public sealed class ConcurrentIdempotencyTests : IClassFixture<WalletApiFactory>
{
    private readonly WalletApiFactory _factory;

    public ConcurrentIdempotencyTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    // Same idempotency key, same payload, 2 concurrent deposits.
    // Expected: 1 financial effect; both replay the same response OR one returns 503 IDEMPOTENCY_IN_PROGRESS.
    [Fact]
    public async Task ConcurrentDepositsWithSameKey_ApplyOneEffectAndReturnOneResponse()
    {
        var alice = WalletApiRequests.ClientFor(_factory, "alice");
        var key = WalletApiRequests.NewKey("deposit");

        var balanceBefore = await WalletApiRequests.GetBalanceAsync(alice);

        var responses = await WalletApiRequests.SendTogetherAsync(
            () => WalletApiRequests.SendDepositAsync(alice, "45.00", key),
            () => WalletApiRequests.SendDepositAsync(alice, "45.00", key));

        foreach (var response in responses)
        {
            Assert.True(
                response.StatusCode is HttpStatusCode.Created or HttpStatusCode.ServiceUnavailable,
                $"Unexpected status: {response.StatusCode}");
        }

        foreach (var unavailable in responses.Where(r => r.StatusCode == HttpStatusCode.ServiceUnavailable))
        {
            Assert.Equal("IDEMPOTENCY_IN_PROGRESS", await WalletApiRequests.GetErrorCodeAsync(unavailable));
        }

        var accepted = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToArray();
        Assert.NotEmpty(accepted);

        var bodies = await Task.WhenAll(accepted.Select(r => r.Content.ReadAsStringAsync()));
        Assert.All(bodies, body => Assert.Equal(bodies[0], body));

        Assert.Equal(balanceBefore + 45.00m, await WalletApiRequests.GetBalanceAsync(alice));
        Assert.Equal(1, await WalletApiRequests.CountDepositsAsync(_factory, 45.00m));
        Assert.Equal(1, await WalletApiRequests.CountIdempotencyRecordsAsync(_factory, key));
    }
}
