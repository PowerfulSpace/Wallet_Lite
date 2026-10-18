using System.Net;
using PS.Wallet.IntegrationTests.Endpoints;
using Xunit;

namespace PS.Wallet.IntegrationTests.Concurrency;

public sealed class OppositeDirectionTransferTests : IClassFixture<WalletApiFactory>
{
    private readonly WalletApiFactory _factory;

    public OppositeDirectionTransferTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    // Alice $500, Bob $500.
    // Concurrent: Alice->Bob $100, Bob->Alice $100.
    // Expected: both succeed OR one succeeds (deadlock victim retried); no partial; balances consistent.
    [Fact]
    public async Task OppositeDirectionTransfers_CompleteWithoutPartialState()
    {
        var alice = WalletApiRequests.ClientFor(_factory, "alice");
        var bob = WalletApiRequests.ClientFor(_factory, "bob");

        Assert.Equal(500.00m, await WalletApiRequests.FundAsync(alice, "500.00"));
        Assert.Equal(500.00m, await WalletApiRequests.FundAsync(bob, "500.00"));

        var responses = await WalletApiRequests.SendTogetherAsync(
            () => WalletApiRequests.SendTransferAsync(alice, "bob", "100.00", WalletApiRequests.NewKey("opposite")),
            () => WalletApiRequests.SendTransferAsync(bob, "alice", "100.00", WalletApiRequests.NewKey("opposite")));

        foreach (var response in responses)
        {
            Assert.True(
                response.StatusCode is HttpStatusCode.Created or HttpStatusCode.ServiceUnavailable,
                $"Unexpected status: {response.StatusCode}");
        }

        // A request that lost the lock-ordering race may surface as an uncertain commit (503) instead of
        // succeeding, so one or two transfers may be recorded, but the sum of both wallets must stay fixed.
        var accepted = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.InRange(accepted, 1, 2);

        var aliceBalance = await WalletApiRequests.GetBalanceAsync(alice);
        var bobBalance = await WalletApiRequests.GetBalanceAsync(bob);

        Assert.True(aliceBalance >= 0m && bobBalance >= 0m);
        Assert.Equal(1000.00m, aliceBalance + bobBalance);

        // No partial transfer: every recorded transfer owns exactly two ledger entries, and the ledger sums
        // equal the reported balances.
        Assert.Equal(accepted, await WalletApiRequests.CountTransfersAsync(_factory));
        Assert.Equal(accepted * 2, await WalletApiRequests.CountTransferLedgerEntriesAsync(_factory));
        Assert.Equal(aliceBalance, await WalletApiRequests.WalletLedgerSumAsync(_factory, "alice"));
        Assert.Equal(bobBalance, await WalletApiRequests.WalletLedgerSumAsync(_factory, "bob"));
    }
}
