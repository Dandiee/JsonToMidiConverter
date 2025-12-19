using Api.Generators;
using Api.Models.Enums;

namespace Api.Models;

[AutoSerialize]
public sealed partial class RawMeasure : Serializable
{
    public int Index { get; set; }
    public List<RawVoice> Voices { get; set; } = [];
    public MusicalFraction  Signature { get; set; } = MusicalFraction.Zero;
    public List<byte> AlternateEnding { get; set; } = [];

    public DisplayText? Marker { get; set; }
    public MeasureTempo? Tempo { get; set; }

    public TripletFeel TripletFeel { get; set; }

    public byte Repeat { get; set; }

    public bool Rest { get; set; }
    public bool RepeatStart { get; set; }
    public bool DoubleBarLine { get; set; }

    //public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    byte flags = ReadByte(buffer, ref cursor);
    //    Rest = (flags & 1) != 0;
    //    RepeatStart = (flags & (1 << 1)) != 0;
    //    DoubleBarLine = (flags & (1 << 2)) != 0;

    //    Index = (int)ReadUInt32(buffer, ref cursor);
    //    Voices = ReadList<RawVoice>(buffer, ref cursor);
    //    Signature = Read<MusicalFraction>(buffer, ref cursor);
    //    AlternateEnding = ReadList(buffer, ref cursor);
    //    Marker = Read<DisplayText>(buffer, ref cursor);
    //    Tempo = Read<MeasureTempo>(buffer, ref cursor);
    //    TripletFeel = (TripletFeel)ReadByte(buffer, ref cursor);
    //    Repeat = ReadByte(buffer, ref cursor);

    //}

    //public override void Write(Span<byte> buffer, ref int cursor)
    //{
    //    byte flags = 0;
    //    flags |= (byte)(Rest ? 1 : 0);
    //    flags |= (byte)(RepeatStart ? 1 << 1 : 0);
    //    flags |= (byte)(DoubleBarLine ? 1 << 2 : 0);
    //    Write(buffer, ref cursor, flags);
    //    Write(buffer, ref cursor, Index);
    //    Write(buffer, ref cursor, Voices);
    //    Write(buffer, ref cursor, Signature);
    //    Write(buffer, ref cursor, AlternateEnding);
    //    Write(buffer, ref cursor, Marker);
    //    Write(buffer, ref cursor, Tempo);
    //    Write(buffer, ref cursor, (byte)TripletFeel);
    //    Write(buffer, ref cursor, Repeat);
    //}
}