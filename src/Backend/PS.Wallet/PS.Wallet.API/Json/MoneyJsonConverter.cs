using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PS.Wallet.API.Json;

public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new JsonException("Monetary value must be a non-empty decimal string.");
            }

            if (!decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                throw new JsonException("Monetary value must be a valid decimal string using a period as the decimal separator.");
            }

            return value;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            throw new JsonException("Monetary value must be a JSON string, not a JSON number.");
        }

        throw new JsonException("Monetary value must be a JSON string.");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("0.00", CultureInfo.InvariantCulture));
    }
}
