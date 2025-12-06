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
    [JsonIgnore] public int OriginalIndex { get; set; }
    [JsonIgnore] public TimeSignature Sgntr { get; set; }

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
        if (Is("P2", "ride"))
        {

        }

        var startTime = TimeConverter.ConvertTo<MetricTimeSpan>(new BarBeatFractionTimeSpan(Index), Part.TempoMap);
        StartTime = new Time(TimeConverter.ConvertFrom(startTime, Part.TempoMap));

        if (Part.Anacrusis)
        {
            if (Index == 1)
            {
                var firstMeasureActualLength = Part.Measures[0].Voices[0].Beats.Sum(e => e.GetDuration().Tick);
                var firstMeasureExpectedLength = Part.Measures[1].StartTime - Part.Measures[0].StartTime;
                Part.AnacrusisOffset = firstMeasureExpectedLength - firstMeasureActualLength;
            }
            if (Index > 0)
            {
                StartTime -= Part.AnacrusisOffset;
            }
        }

        if (Signature.Count == 2)
        {
            SignatureNominator = (byte)Signature[0];
            SignatureDenominator = (byte)Signature[1];
        }

        Voices.ForEach(v => v.Build());

        
    }


    

    public override string ToString() => $"M{Index} {Part}";

    public bool Is(string name, string? filter = null)
    {
        if (string.IsNullOrEmpty(name)) return false;

        var trimmed = name.Trim().ToUpperInvariant();
        var isMatching = trimmed[0] switch
        {
            'M' => $"{this}".Equals(trimmed),
            'P' => $"{Part}".Equals(trimmed),
            _ => false
        };

        return isMatching && (string.IsNullOrEmpty(filter) || Part.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }
}