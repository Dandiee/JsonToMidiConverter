using System.Diagnostics;
using System.Text.Json.Serialization;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed partial class Measure
{
    [JsonIgnore]public int Index { get; private set; }
    [JsonIgnore]public Part Part { get; private set; }
    [JsonIgnore]public Song Song => Part.Song;
    [JsonIgnore] public Time StartTime { get; private set; }
    [JsonIgnore] public byte? SignatureNominator { get; private set; }
    [JsonIgnore] public byte? SignatureDenominator { get; private set; }
    [JsonIgnore] public Measure? Next { get; private set; }
    [JsonIgnore] public Measure? Previous { get; private set; }

    public void SetNavigation(Part part, int index)
    {
        Index = index;
        Part = part;

        if (Index > 0)
        {
            Previous = Part.Measures[Index - 1];
            Previous.Next = this;
        }

        for (var i = 0; i < Voices.Count; i++)
        {
            Voices[i].SetNavigation(this, i);
        }
    }

    public void Build()
    {
        var startTime = TimeConverter.ConvertTo<MetricTimeSpan>(new BarBeatFractionTimeSpan(Index), Part.TempoMap);
        StartTime = new Time(TimeConverter.ConvertFrom(startTime, Part.TempoMap));

        if (Signature.Count == 2)
        {
            SignatureNominator = (byte)Signature[0];
            SignatureDenominator = (byte)Signature[1];
        }

        Voices.ForEach(v => v.Build());
    }

    public override string ToString() => $"M{Index} {Part}";

    public bool Is(string name) => name == $"{this}";
}