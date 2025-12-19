using Api.Generators;

namespace Api.Models;

[AutoSerialize]
public sealed partial class CapoPartial : Serializable
{
    public List<byte> Strings { get; set; } = [];
    public byte Fret { get; set; }
    
    //public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    Strings = ReadList(buffer, ref cursor);
    //    Fret = ReadByte(buffer, ref cursor);
    //}

    //public override void Write(Span<byte> buffer, ref int cursor)
    //{
    //    Write(buffer, ref cursor, Strings);
    //    Write(buffer, ref cursor, Fret);
    //}
}