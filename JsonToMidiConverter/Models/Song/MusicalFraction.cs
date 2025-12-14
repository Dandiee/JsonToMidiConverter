namespace JsonToMidiConverter.Models.Song;

public class MusicalFraction
{
    public byte Numerator { get; set; }
    public byte Denominator { get; set; }

    public MusicalFraction() { }
    public MusicalFraction(byte numerator, byte denominator)
    {
        Numerator = numerator;
        Denominator = denominator;
    }

    public MusicalFraction Copy() => new(Numerator, Denominator);
}