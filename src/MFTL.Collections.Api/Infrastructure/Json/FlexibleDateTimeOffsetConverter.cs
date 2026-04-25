using System.Text.Json;
using System.Text.Json.Serialization;

namespace MFTL.Collections.Api.Infrastructure.Json;

public class FlexibleDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string but got {reader.TokenType}");

        var stringValue = reader.GetString();
        if (string.IsNullOrWhiteSpace(stringValue))
            return null;

        if (DateTimeOffset.TryParse(stringValue, out var result))
            return result;

        // Fallback for YYYY-MM-DD
        if (DateTime.TryParse(stringValue, out var dt))
            return new DateTimeOffset(dt, TimeSpan.Zero);

        throw new JsonException($"Unable to parse '{stringValue}' as DateTimeOffset.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString("O"));
        else
            writer.WriteNullValue();
    }
}
