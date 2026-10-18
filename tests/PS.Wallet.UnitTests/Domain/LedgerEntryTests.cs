using PS.Wallet.Domain;
using PS.Wallet.Domain.Entities;
using Xunit;

namespace PS.Wallet.UnitTests.Domain;

public sealed class LedgerEntryTests
{
    [Fact]
    public void LedgerEntry_ForDeposit_ShouldHaveDepositIdAndNullTransferId()
    {
        var entry = LedgerEntry.ForDeposit(Guid.NewGuid(), Guid.NewGuid(), 10m, DateTime.UtcNow);
        Assert.NotNull(entry.DepositId);
        Assert.Null(entry.TransferId);
        Assert.Equal(10m, entry.Amount);
    }

    [Fact]
    public void LedgerEntry_ForTransfer_ShouldHaveTransferIdAndNullDepositId()
    {
        var entry = LedgerEntry.ForTransfer(Guid.NewGuid(), Guid.NewGuid(), -10m, DateTime.UtcNow);
        Assert.Null(entry.DepositId);
        Assert.NotNull(entry.TransferId);
        Assert.Equal(-10m, entry.Amount);
    }

    [Fact]
    public void LedgerEntry_ForTransfer_ZeroAmount_ShouldThrow()
    {
        Assert.Throws<DomainException>(() =>
            LedgerEntry.ForTransfer(Guid.NewGuid(), Guid.NewGuid(), 0m, DateTime.UtcNow));
    }
}
