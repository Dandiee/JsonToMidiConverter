namespace JsonToMidiConverter.Models.Song;

public sealed class MusicalFraction
{
    public byte Numerator { get; set; }
    public byte Denominator { get; set; }

    public MusicalFraction Copy() => new()
    {
        Numerator = Numerator, 
        Denominator = Denominator
    };
}