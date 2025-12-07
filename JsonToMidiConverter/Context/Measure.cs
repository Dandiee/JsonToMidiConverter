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
    [JsonIgnore] public byte SignatureNominator { get; private set; }
    [JsonIgnore] public byte SignatureDenominator { get; private set; }
    [JsonIgnore] public Measure? Next { get; private set; }
    [JsonIgnore] public Measure? Previous { get; private set; }
    [JsonIgnore] public int OriginalIndex { get; set; }
    [JsonIgnore] public TimeSignature Sgntr { get; set; }
    [JsonIgnore] public Time Duration { get; set; }
    [JsonIgnore] public Time End { get; set; }
    [JsonIgnore] public Time Start { get; set; }

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
        if (Signature.Count == 2)
        {
            SignatureNominator = (byte)Signature[0];
            SignatureDenominator = (byte)Signature[1];
        }
        else
        {
            SignatureNominator = Previous!.SignatureNominator;
            SignatureDenominator = Previous.SignatureDenominator;
        }

        Start = Previous?.End ?? new Time();
        Duration = Part.Anacrusis && Index == 0 
            ? new Time(Voices[0].Beats.Where(e => string.IsNullOrEmpty(e.GraceNote)).Sum(e => e.GetDuration().Tick))
            : new Time(SignatureNominator, SignatureDenominator);
        End = Start + Duration;

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