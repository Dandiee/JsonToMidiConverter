using Api.Generators;

namespace Api.Models;

[AutoSerialize]
public partial class MeasureTempo : Serializable
{
    public int Type { get; set; }
    public int Bpm { get; set; }
    public int Progressive { get; set; }

    //public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    Type = (int)ReadUInt32(buffer, ref cursor);
    //    Bpm = (int)ReadUInt32(buffer, ref cursor);
    //    Progressive = (int)ReadUInt32(buffer, ref cursor);
    //}

    //public override void Write(Span<byte> buffer, ref int cursor)
    //{
    //    Write(buffer, ref cursor, (uint)Type);
    //    Write(buffer, ref cursor, (uint)Bpm);
    //    Write(buffer, ref cursor, (uint)Progressive);
    //}
}