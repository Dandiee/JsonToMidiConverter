namespace Persistence.Models;

public struct MusicalFraction
{
    public ushort Nominator;
    public ushort Denominator;

    public MusicalFraction(ushort nominator, ushort denominator)
    {
        Nominator = nominator;
        Denominator = denominator;
    }

    public MusicalFraction(List<ushort> array)
    {
        if (array.Count != 2) throw new ArgumentException("Array must contain exactly two elements.");

        Nominator = array[0];
        Denominator = array[1];
    }

    public static MusicalFraction? Create(List<ushort> array)
    {
        if (array.Count == 0) return null;

        return new MusicalFraction(array);
    }
}