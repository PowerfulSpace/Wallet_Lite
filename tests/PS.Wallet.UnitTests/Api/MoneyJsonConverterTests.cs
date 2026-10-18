using System.Text.Json;
using PS.Wallet.API.Json;
using Xunit;

namespace PS.Wallet.UnitTests.Api;

public sealed class MoneyJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new MoneyJsonConverter() },
    };

    private sealed record MoneyModel(decimal Amount);

    [Theory]
    [InlineData(125.5, "\"125.50\"")]
    [InlineData(-25, "\"-25.00\"")]
    [InlineData(0, "\"0.00\"")]
    [InlineData(1000000.00, "\"1000000.00\"")]
    public void Write_AlwaysProducesTwoDecimalString(decimal value, string expected)
    {
        Assert.Equal(expected, JsonSerializer.Serialize(value, Options));
    }

    [Theory]
    [InlineData("\"50.00\"", 50.00)]
    [InlineData("\"0.01\"", 0.01)]
    [InlineData("\"-25.50\"", -25.50)]
    public void Read_DecimalString_ReturnsValue(string json, decimal expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<decimal>(json, Options));
    }

    [Theory]
    [InlineData("50.00")]
    [InlineData("-25.50")]
    [InlineData("0")]
    public void Read_JsonNumber_Throws(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal>(json, Options));
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("\"abc\"")]
    [InlineData("\"1 000.00\"")]
    public void Read_NonDecimalString_Throws(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal>(json, Options));
    }

    [Fact]
    public void Serialize_ObjectWithDecimal_WritesString()
    {
        Assert.Equal("{\"Amount\":\"12.30\"}", JsonSerializer.Serialize(new MoneyModel(12.3m), Options));
    }

    [Fact]
    public void Deserialize_ObjectWithDecimalString_ReadsValue()
    {
        Assert.Equal(12.30m, JsonSerializer.Deserialize<MoneyModel>("{\"Amount\":\"12.30\"}", Options)!.Amount);
    }
}
