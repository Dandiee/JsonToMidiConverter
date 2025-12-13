namespace JsonToMidiConverter.Models.Song;

public record MusicalFraction(byte Numerator, byte Denominator)
{
    public MusicalFraction Copy() => new(Numerator, Denominator);
}