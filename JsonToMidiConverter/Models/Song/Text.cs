using JsonToMidiConverter.Models.Song.JsonConverters;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

public sealed class Text : ISerializable
{
    [JsonPropertyName("text")]
    public string Content { get; set; } = string.Empty;

    [JsonConverter(typeof(NullToDefaultConverter<int>))]
    public int Width { get; set; } = 0;

    
    public Text Clone() => new()
    {
        Content = Content,
        Width = Width
    };
}