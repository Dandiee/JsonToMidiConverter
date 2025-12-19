using Api.Generators;
using Api.Models.Enums;

namespace Api.Models;

[AutoSerialize]
public sealed partial class HarmonicData : Serializable
{
    public HarmonicType Type { get; set; }
    public string Note { get; set; } = string.Empty;
    public byte Shift { get; set; }
    public sbyte Fret { get; set; }

    //public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    Type = (HarmonicType)ReadByte(buffer, ref cursor);
    //    Note = ReadString(buffer, ref cursor);
    //    Shift = ReadByte(buffer, ref cursor);
    //    Fret = (sbyte)ReadByte(buffer, ref cursor);
    //}

    //public override void Write(Span<byte> buffer, ref int cursor)
    //{
    //    Write(buffer, ref cursor, (byte)Type);
    //    Write(buffer, ref cursor, Note);
    //    Write(buffer, ref cursor, Shift);
    //    Write(buffer, ref cursor, Fret);
    //}
}