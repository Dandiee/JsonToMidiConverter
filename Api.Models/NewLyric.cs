using Api.Generators;

namespace Api.Models;

[AutoSerialize]
public sealed partial class NewLyric : Serializable
{
    public int Line { get; set; }
    public int Offset { get; set; }
    public string Text { get; set; } = string.Empty;

    //public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    Line = (int)ReadUInt32(buffer, ref cursor);
    //    Offset = (int)ReadUInt32(buffer, ref cursor);
    //    Text = ReadString(buffer, ref cursor);
    //}

    //public override void Write(Span<byte> buffer, ref int cursor)
    //{
    //    Write(buffer, ref cursor, Line);
    //    Write(buffer, ref cursor, Offset);
    //    Write(buffer, ref cursor, Text);
    //}
}