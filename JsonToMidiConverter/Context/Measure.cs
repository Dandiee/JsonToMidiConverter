using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Context;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed class Measure : MeasureRaw, IMusicalElement<Measure>
{
    public Measure(MeasureRaw raw)
    {
        this.Bootstrap(raw);
        Voices = raw.VoicesRaw.Select(e => new Voice(e)).ToList();
    }

    public List<Voice> Voices { get; set; } = [];

    public Song Song => Part.Song;
    public int OriginalIndex { get; set; }
    public Time Signature { get; set; }
    public int RepeatIndex { get; set; }
    public int Bpm { get; set; }

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
        Start = Previous?.End ?? new Time();
        Duration = Part.Anacrusis && Index == 0 
            ? new Time(Voices[0].Beats.Where(e => !e.GraceNote.HasValue).Sum(e => e.Duration.Tick))
            : Signature;
        End = Start + Duration;
        Bpm = (int)Math.Round(Part.TempoMap.GetTempoAtTime(Start.Span).BeatsPerMinute);
        Voices.ForEach(v => v.Build());
    }


    public override string ToString() => $"M{Index} {Part}";

    public Part Part { get; set; }
    public int Index { get; set; }
    public Measure? Next { get; set; }
    public Measure? Previous { get; set; }
    public Time Start { get; set; }
    public Time End { get; set; }
    public Time Duration { get; set; }
}