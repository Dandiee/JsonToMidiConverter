using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dani.Data.Json.Converters;

public abstract class MappedEnumConverter<T> : JsonConverter<T>
{
    protected abstract IReadOnlyDictionary<string, T> Mapping { get; }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => Mapping[reader.GetString()!],

            _ => throw new NotSupportedException($"Unexpected token type for {typeof(T).Name}: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => throw new NotImplementedException();
}