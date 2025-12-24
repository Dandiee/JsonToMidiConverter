using System.Diagnostics;
using Dani.Data.Models.Enums;
using Dani.Data.Models.Parts;

using DataMeasure = Dani.Data.Models.Parts.Measure;

namespace Dani.Converter.Models;

[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed class Measure : MusicalElement<Measure, Part>
{
    public List<Voice> Voices { get; }

    public TripletFeel TripletFeel { get; }
    public Time Signature { get; }
    public MusicalFraction SignatureFracture { get; }
    public bool RepeatStart { get; }
    public int Repeat { get; }
    public List<byte> AlternateEnding { get; }
    public int Bpm { get; }
    
    public Measure(Part part, DataMeasure data, int index) 
        : base(part, part, index)
    {
        Signature = data.Signature.IsZero() ? Previous!.Signature : data.Signature.ToTime();
        TripletFeel = data.TripletFeel;
        RepeatStart = data.RepeatStart;
        Repeat = data.Repeat;
        AlternateEnding = data.AlternateEnding;
        Bpm = (int)Math.Round(Part.TempoMap.GetTempoAtTime(Start.Span).BeatsPerMinute);
        
        SignatureFracture = data.Signature.IsZero() ? Previous!.SignatureFracture : data.Signature;

        Voices = new List<Voice>(data.Voices.Count);
        for (var i = 0; i < data.Voices.Count; i++)
        {
            Voices.Add(new Voice(this, data.Voices[i], i));
        }
    }

    protected override Measure? GetPrevious(object? state = null) 
        => Index > 0 ? Part.Measures[Index - 1] : null;

    public void Build()
    {
        Start = Previous?.End ?? new Time();
        Duration = Part.Anacrusis && Index == 0 
            ? new Time(Voices[0].Beats.Where(e => e.GraceNote == GraceNote.None).Sum(e => e.Duration.Tick))
            : Signature;
        End = Start + Duration;
        
        Voices.ForEach(v => v.Build());
    }

    public override string ToString() => $"M{Index} {Part}";
}