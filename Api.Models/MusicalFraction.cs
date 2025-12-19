using Api.Models.Converters;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Api.Generators;

namespace Api.Models;

[JsonConverter(typeof(MusicalFractionConverter))]
[AutoSerialize]
public sealed partial class MusicalFraction(byte nominator, byte denominator) : Serializable
{
    public static readonly MusicalFraction Zero = new(0, 0);

    public byte Nominator { get; private set; } = nominator;
    public byte Denominator { get; private set; } = denominator;

    public MusicalFraction() : this(0, 0) { }

    public static MusicalFraction? Create(List<ushort> array)
    {
        if (array.Count != 2) return null;
        return new MusicalFraction((byte)array[0], (byte)array[1]);
    }

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void Write(Span<byte> buffer, ref int cursor)
    //{
    //    // Write 2 bytes
    //    buffer[cursor++] = Nominator;
    //    buffer[cursor++] = Denominator;
    //}

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    Nominator = buffer[cursor++];
    //    Denominator = buffer[cursor++];
    //}
}