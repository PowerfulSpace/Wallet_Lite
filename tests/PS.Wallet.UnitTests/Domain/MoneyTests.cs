using PS.Wallet.Domain;
using PS.Wallet.Domain.ValueObjects;
using Xunit;

namespace PS.Wallet.UnitTests.Domain;

public sealed class MoneyTests
{
    [Theory]
    [InlineData(0.01)]
    [InlineData(1.00)]
    [InlineData(1000000.00)]
    public void Money_ValidAmount_ShouldSucceed(decimal amount)
    {
        var money = new Money(amount);
        Assert.Equal(amount, money.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Money_NonPositive_ShouldThrow(decimal amount)
    {
        Assert.Throws<DomainException>(() => new Money(amount));
    }

    [Fact]
    public void Money_AboveMax_ShouldThrow()
    {
        Assert.Throws<DomainException>(() => new Money(1000000.01m));
    }

    [Theory]
    [InlineData(1.001)]
    [InlineData(0.005)]
    public void Money_TooManyFractionalDigits_ShouldThrow(decimal amount)
    {
        Assert.Throws<DomainException>(() => new Money(amount));
    }

    [Fact]
    public void Money_ToString_ShouldUseInvariantTwoDecimals()
    {
        Assert.Equal("25.50", new Money(25.50m).ToString());
    }
}
