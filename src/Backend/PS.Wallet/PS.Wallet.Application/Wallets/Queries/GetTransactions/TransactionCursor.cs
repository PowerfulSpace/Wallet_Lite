using System.Text;
using System.Text.Json;

namespace PS.Wallet.Application.Wallets.Queries.GetTransactions;

internal sealed record TransactionCursor(DateTime CreatedAtUtc, Guid Id)
{
    public string Encode()
    {
        var json = JsonSerializer.Serialize(new { createdAtUtc = CreatedAtUtc, id = Id });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static TransactionCursor Decode(string value)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var createdAtUtc = root.GetProperty("createdAtUtc").GetDateTime();
            var id = root.GetProperty("id").GetGuid();
            return new TransactionCursor(createdAtUtc, id);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidCursorException("The pagination cursor is invalid.");
        }
    }
}
