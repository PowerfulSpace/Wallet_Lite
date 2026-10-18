using PS.Wallet.Application;
using PS.Wallet.Application.Wallets.Queries.GetTransactions;
using Xunit;

namespace PS.Wallet.UnitTests.Application;

public sealed class TransactionCursorTests
{
    [Fact]
    public void EncodeDecode_RoundTrips()
    {
        var cursor = new TransactionCursor(
            new DateTime(2026, 10, 18, 10, 20, 30, 123, DateTimeKind.Utc),
            Guid.NewGuid());

        var decoded = TransactionCursor.Decode(cursor.Encode());

        Assert.Equal(cursor.CreatedAtUtc, decoded.CreatedAtUtc);
        Assert.Equal(cursor.Id, decoded.Id);
    }

    [Fact]
    public void Encode_MatchesDocumentedFormat()
    {
        var createdAtUtc = new DateTime(2026, 10, 18, 10, 20, 30, DateTimeKind.Utc);
        var id = Guid.NewGuid();

        var encoded = new TransactionCursor(createdAtUtc, id).Encode();

        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        Assert.Contains("\"createdAtUtc\":\"2026-10-18T10:20:30Z\"", json);
        Assert.Contains($"\"id\":\"{id}\"", json);
    }

    [Theory]
    [InlineData("not a base64 value!")]
    [InlineData("not-a-valid-cursor")]
    public void Decode_InvalidBase64_ThrowsInvalidCursor(string value)
    {
        Assert.Throws<InvalidCursorException>(() => TransactionCursor.Decode(value));
    }

    [Fact]
    public void Decode_ValidBase64InvalidJson_ThrowsInvalidCursor()
    {
        var value = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("this is not json"));

        Assert.Throws<InvalidCursorException>(() => TransactionCursor.Decode(value));
    }

    [Fact]
    public void Decode_JsonMissingProperties_ThrowsInvalidCursor()
    {
        var value = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"foo\":\"bar\"}"));

        Assert.Throws<InvalidCursorException>(() => TransactionCursor.Decode(value));
    }

    [Fact]
    public void Decode_JsonWithWrongTypes_ThrowsInvalidCursor()
    {
        var value = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("{\"createdAtUtc\":\"not-a-date\",\"id\":\"not-a-guid\"}"));

        Assert.Throws<InvalidCursorException>(() => TransactionCursor.Decode(value));
    }
}
