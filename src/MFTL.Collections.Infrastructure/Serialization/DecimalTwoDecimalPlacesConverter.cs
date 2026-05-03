using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace MFTL.Collections.Infrastructure.Serialization;

public class DecimalTwoDecimalPlacesConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(decimal.Parse(value.ToString("F2", CultureInfo.InvariantCulture)));
    }
}
