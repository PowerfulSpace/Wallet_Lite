using System.Net;
using PS.Wallet.IntegrationTests.Endpoints;
using Xunit;

namespace PS.Wallet.IntegrationTests.Concurrency;

public sealed class ConcurrentDebitTests : IClassFixture<WalletApiFactory>
{
    private readonly WalletApiFactory _factory;

    public ConcurrentDebitTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    // Alice $100, Bob $0.
    // Two concurrent transfers: $80 and $70.
    // Expected: at most one succeeds; Alice balance >= 0; no partial transfer.
    [Fact]
    public async Task TwoConcurrentTransfersExceedingBalance_ApplyAtMostOneAndNeverGoNegative()
    {
        var alice = WalletApiRequests.ClientFor(_factory, "alice");
        var bob = WalletApiRequests.ClientFor(_factory, "bob");

        Assert.Equal(100.00m, await WalletApiRequests.FundAsync(alice, "100.00"));

        var responses = await WalletApiRequests.SendTogetherAsync(
            () => WalletApiRequests.SendTransferAsync(alice, "bob", "80.00", WalletApiRequests.NewKey("debit")),
            () => WalletApiRequests.SendTransferAsync(alice, "bob", "70.00", WalletApiRequests.NewKey("debit")));

        var accepted = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToArray();
        var rejected = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToArray();

        Assert.Single(accepted);
        var conflict = Assert.Single(rejected);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("INSUFFICIENT_FUNDS", await WalletApiRequests.GetErrorCodeAsync(conflict));

        var aliceBalance = await WalletApiRequests.GetBalanceAsync(alice);
        var bobBalance = await WalletApiRequests.GetBalanceAsync(bob);

        Assert.True(aliceBalance >= 0m, $"Alice balance must never be negative, but was {aliceBalance}.");
        Assert.Contains(aliceBalance, new[] { 20.00m, 30.00m });
        Assert.Equal(100.00m, aliceBalance + bobBalance);

        // No partial transfer: one transfer, two ledger entries, and ledger sums that match both balances.
        Assert.Equal(1, await WalletApiRequests.CountTransfersAsync(_factory));
        Assert.Equal(2, await WalletApiRequests.CountTransferLedgerEntriesAsync(_factory));
        Assert.Equal(aliceBalance, await WalletApiRequests.WalletLedgerSumAsync(_factory, "alice"));
        Assert.Equal(bobBalance, await WalletApiRequests.WalletLedgerSumAsync(_factory, "bob"));
    }
}
