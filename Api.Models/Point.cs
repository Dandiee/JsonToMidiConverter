using Api.Generators;
using static System.Net.Mime.MediaTypeNames;

namespace Api.Models;

[AutoSerialize]
public sealed partial class Point : Serializable
{
    public float Position { get; set; } // 0 - 60
    public float Tone { get; set; } // -800 - 600
    public byte Vibrato { get; set; } // TODO: not all types have vibrato

    //public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    Position = ReadSingle(buffer, ref cursor);
    //    Tone = ReadSingle(buffer, ref cursor);
    //    Vibrato = ReadByte(buffer, ref cursor);
    //}

    //public override void Write(Span<byte> buffer, ref int cursor)
    //{
    //    Write(buffer, ref cursor, Position);
    //    Write(buffer, ref cursor, Tone);
    //    Write(buffer, ref cursor, Vibrato);
    //}
}