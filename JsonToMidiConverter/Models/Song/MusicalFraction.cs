namespace JsonToMidiConverter.Models.Song;

public class MusicalFraction : ISerializable
{
    public byte Numerator { get; set; } // 1 - 229
    public byte Denominator { get; set; } // 1 - *

    public MusicalFraction() { }
    public MusicalFraction(byte numerator, byte denominator)
    {
        Numerator = numerator;
        Denominator = denominator;
    }

    public MusicalFraction Copy() => new(Numerator, Denominator);
}