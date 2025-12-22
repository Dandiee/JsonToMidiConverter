using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dani.Data.Json.Converters;

public class NullToDefaultConverter<T> : JsonConverter<T> where T : struct
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => default,
            _ => JsonSerializer.Deserialize<T>(ref reader),
        };

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
     => throw new NotImplementedException();
}