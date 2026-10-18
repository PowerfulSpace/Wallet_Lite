using System.Net;
using PS.Wallet.IntegrationTests.Endpoints;
using Xunit;

namespace PS.Wallet.IntegrationTests.Concurrency;

public sealed class ConcurrentCreditTests : IClassFixture<WalletApiFactory>
{
    private readonly WalletApiFactory _factory;

    public ConcurrentCreditTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    // Bob $999,950.
    // Two concurrent deposits: $30 each.
    // Expected: at most one succeeds; Bob balance <= 1,000,000.00.
    [Fact]
    public async Task TwoConcurrentDepositsAboveBalanceLimit_ApplyAtMostOneAndStayWithinLimit()
    {
        var bob = WalletApiRequests.ClientFor(_factory, "bob");

        Assert.Equal(999950.00m, await WalletApiRequests.FundAsync(bob, "999950.00"));

        var responses = await WalletApiRequests.SendTogetherAsync(
            () => WalletApiRequests.SendDepositAsync(bob, "30.00", WalletApiRequests.NewKey("credit")),
            () => WalletApiRequests.SendDepositAsync(bob, "30.00", WalletApiRequests.NewKey("credit")));

        var accepted = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToArray();
        var rejected = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToArray();

        Assert.Single(accepted);
        var conflict = Assert.Single(rejected);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("BALANCE_LIMIT_EXCEEDED", await WalletApiRequests.GetErrorCodeAsync(conflict));

        var balance = await WalletApiRequests.GetBalanceAsync(bob);

        Assert.True(balance <= 1000000.00m, $"Bob balance must never exceed the maximum, but was {balance}.");
        Assert.Equal(999980.00m, balance);
        Assert.Equal(1, await WalletApiRequests.CountDepositsAsync(_factory, 30.00m));
        Assert.Equal(balance, await WalletApiRequests.WalletLedgerSumAsync(_factory, "bob"));
    }
}
