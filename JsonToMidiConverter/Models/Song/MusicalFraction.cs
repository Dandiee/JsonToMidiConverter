namespace JsonToMidiConverter.Models.Song;

public record MusicalFraction(long Numerator, long Denominator)
{
    public MusicalFraction Copy() => new(Numerator, Denominator);
}