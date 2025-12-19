using Api.Generators;
using Api.Models.Enums;

namespace Api.Models;

[AutoSerialize]
public sealed partial class BrushStroke : Serializable
{
    public Direction Direction { get; set; }
    public short Duration { get; set; }
    public float Shift { get; set; } // there's a record with value "28.000001" maybe we can just normalize the data
    
    //public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    Direction = (Direction)ReadByte(buffer, ref cursor);
    //    Duration = (short)ReadUInt16(buffer, ref cursor);
    //    Shift = ReadSingle(buffer, ref cursor);
    //}

    //public override void Write(Span<byte> buffer, ref int cursor)
    //{
    //    Write(buffer, ref cursor, (byte)Direction);
    //    Write(buffer, ref cursor, Duration);
    //    Write(buffer, ref cursor, Shift);
    //}
}