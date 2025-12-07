using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Context;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed partial class Measure : MusicalElement<Measure>
{
    [JsonIgnore]public Song Song => Part.Song;
    [JsonIgnore] public byte SignatureNominator { get; private set; }
    [JsonIgnore] public byte SignatureDenominator { get; private set; }
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
            ? new Time(Voices[0].Beats.Where(e => string.IsNullOrEmpty(e.GraceNote)).Sum(e => e.Duration.Tick))
            : new Time(SignatureNominator, SignatureDenominator);
        End = Start + Duration;

        Voices.ForEach(v => v.Build());
    }


    public override string ToString() => $"M{Index} {Part}";
}