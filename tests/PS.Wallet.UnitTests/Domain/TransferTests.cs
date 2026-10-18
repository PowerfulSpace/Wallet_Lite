using PS.Wallet.Domain;
using PS.Wallet.Domain.Entities;
using Xunit;

namespace PS.Wallet.UnitTests.Domain;

public sealed class TransferTests
{
    [Fact]
    public void Transfer_SelfTransfer_ShouldThrow()
    {
        var walletId = Guid.NewGuid();
        Assert.Throws<DomainException>(() =>
            Transfer.Create(walletId, walletId, 10m, DateTime.UtcNow));
    }

    [Fact]
    public void Transfer_Valid_ShouldSucceed()
    {
        var transfer = Transfer.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, DateTime.UtcNow);
        Assert.Equal(10m, transfer.Amount);
    }

    [Fact]
    public void Transfer_ZeroAmount_ShouldThrow()
    {
        Assert.Throws<DomainException>(() =>
            Transfer.Create(Guid.NewGuid(), Guid.NewGuid(), 0m, DateTime.UtcNow));
    }
}
