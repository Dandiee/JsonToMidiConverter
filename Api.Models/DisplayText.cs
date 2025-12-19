using System.Text.Json.Serialization;
using Api.Models.Converters;

namespace Api.Models;

[JsonConverter(typeof(DisplayTextConverter))]
public sealed class DisplayText : InternalDisplayText;

public class InternalDisplayText
{
    public string Text { get; set; } = string.Empty;
    [JsonConverter(typeof(NullToDefaultConverter<ushort>))] public ushort Width { get; set; }

    internal DisplayText ToModel() => new()
    {
        Text = Text,
        Width = Width
    };
}