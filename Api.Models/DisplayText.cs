using System.Text.Json.Serialization;
using Api.Generators;
using Api.Models.Converters;

namespace Api.Models;

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

    //public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    Text = ReadString(buffer, ref cursor);
    //    Width = ReadUInt16(buffer, ref cursor);
    //}

    //public override void Write(Span<byte> buffer, ref int cursor)
    //{
    //    Write(buffer, ref cursor, Text);
    //    Write(buffer, ref cursor, Width);
    //}
}