using System.Text.Json.Serialization;
using Api.Models.Converters;

namespace Api.Models;

[JsonConverter(typeof(DisplayTextConverter))]
public sealed class DisplayText
{
    public string Text { get; set; } = string.Empty;
    [JsonConverter(typeof(NullToDefaultConverter<ushort>))] public ushort Width { get; set; } // 0 - 2455
}