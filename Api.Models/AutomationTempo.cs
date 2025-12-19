using Api.Generators;

namespace Api.Models;

[AutoSerialize]
public sealed partial class AutomationTempo : MeasureTempo
{
    public ushort Measure { get; set; }
    public float Position { get; set; }
    
    public bool Dotted { get; set; }
    public bool Visible { get; set; }
    public bool Linear { get; set; }

    public string Text { get; set; } = string.Empty;

    //public override void Write(Span<byte> buffer, ref int cursor)
    //{
    //    base.Write(buffer, ref cursor);

    //    byte flags = 0;
    //    flags |= (byte)(Dotted ? 1 : 0);
    //    flags |= (byte)(Visible ? 1 << 1 : 0);
    //    flags |= (byte)(Linear ? 1 << 2 : 0);
    //    Write(buffer, ref cursor, flags);

    //    Write(buffer, ref cursor, Measure);
    //    Write(buffer, ref cursor, Position);
    //    Write(buffer, ref cursor, Text);
    //}

    //public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    base.Read(buffer, ref cursor);

    //    byte flags = ReadByte(buffer, ref cursor);
    //    Dotted = (flags & 1) != 0;
    //    Visible = (flags & (1 << 1)) != 0;
    //    Linear = (flags & (1 << 2)) != 0;

    //    Measure = ReadUInt16(buffer, ref cursor);
    //    Position = ReadSingle(buffer, ref cursor);
    //    Text = ReadString(buffer, ref cursor);
    //}
}