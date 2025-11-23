using System.Diagnostics;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed partial class Measure
{
    [JsonIgnore]public int Index { get; private set; }
    [JsonIgnore]public Part Part { get; private set; }
    [JsonIgnore]public Song Song => Part.Song;
    [JsonIgnore]public Beat[] Beats => Voices.First().Beats;
    [JsonIgnore] public Time StartTime { get; private set; }

    public void Build(Part part, int index)
    {
        Index = index;
        Part = part;
        StartTime = new Time(Index, 0d);

        for (var i = 0; i < Voices.Length; i++)
        {
            Voices[i].Build(this, i);
        }
    }

    public Measure? GetNext()
    {
        if (Index >= Part.Measures.Length - 1)
            return null;

        return Part.Measures[Index + 1];
    }

    public Measure? GetPrevious()
    {
        if (Index <= 0)
            return null;

        return Part.Measures[Index - 1];
    }

}