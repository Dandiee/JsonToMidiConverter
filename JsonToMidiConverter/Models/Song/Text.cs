using JsonToMidiConverter.Models.Song.JsonConverters;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

public sealed class Text : ISerializable
{
    [JsonPropertyName("text")]
    public string Content { get; set; } = string.Empty;

    [JsonConverter(typeof(NullToDefaultConverter<ushort>))]
    public ushort Width { get; set; } // 0 - 2455

    
    public Text Clone() => new()
    {
        Content = Content,
        Width = Width
    };
}