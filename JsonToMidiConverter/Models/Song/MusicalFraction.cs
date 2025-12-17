namespace JsonToMidiConverter.Models.Song;

public class MusicalFraction : ISerializable
{
    public double Numerator { get; set; }
    public double Denominator { get; set; }

    public MusicalFraction() { }
    public MusicalFraction(double numerator, double denominator)
    {
        Numerator = numerator;
        Denominator = denominator;
    }

    public MusicalFraction Copy() => new(Numerator, Denominator);
}