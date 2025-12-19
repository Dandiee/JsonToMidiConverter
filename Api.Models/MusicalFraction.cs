using Api.Models.Converters;
using System.Text.Json.Serialization;

namespace Api.Models;

[JsonConverter(typeof(MusicalFractionConverter))]
public class MusicalFraction
{
    public static readonly MusicalFraction Zero = new(0, 0);

    public ushort Nominator { get; set; }
    public ushort Denominator { get; set; }

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