using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

public sealed class Text
{
    [JsonPropertyName("text")]
    public string Content { get; set; } = string.Empty;
    public int? Width { get; set; }

    public Text Clone() => new()
    {
        Content = Content,
        Width = Width
    };
}