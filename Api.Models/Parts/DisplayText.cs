using System.Text.Json.Serialization;
using Api.Generators;
using Api.Models.Converters;
using Api.Models.Serialization;

namespace Api.Models.Parts;

[JsonConverter(typeof(DisplayTextConverter))]
public sealed class DisplayText : InternalDisplayText;

[JsonSerializable(typeof(InternalDisplayText))]
[AutoSerialize]
public partial class InternalDisplayText : Serializable
{
    public string Text { get; set; } = string.Empty;
    [JsonConverter(typeof(NullToDefaultConverter<ushort>))] 
    public ushort Width { get; set; }

    internal DisplayText ToModel() => new()
    {
        Text = Text,
        Width = Width
    };
}