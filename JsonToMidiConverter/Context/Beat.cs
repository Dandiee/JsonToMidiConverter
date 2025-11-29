using System.Diagnostics;
using System.Text.Json.Serialization;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} V{Voice.Index} M{Measure.Index} P{Part.Index}")]
public sealed partial class Beat
{
    [JsonIgnore] public int Index { get; private set; }
    [JsonIgnore] public Voice Voice { get; private set; }
    [JsonIgnore] public Measure Measure => Voice.Measure;
    [JsonIgnore] public Part Part => Measure.Part;
    [JsonIgnore] public Song Song => Part.Song;
    [JsonIgnore] public Time MusicalDuration { get; private set; }
    [JsonIgnore] public Time RelativeBeatStartTime { get; private set; }
    [JsonIgnore] public Time AbsoluteBeatStartTime { get; private set; }
    [JsonIgnore] public byte Numerator => (byte)Duration[0];
    [JsonIgnore] public byte Denominator => (byte)Duration[1];
    [JsonIgnore] public bool IsAccord { get; private set; }
    [JsonIgnore] public string Nameplate => $"{Index}{Measure.Index}{Part.Index}";
    [JsonIgnore] public Beat? Next { get; private set; }
    [JsonIgnore] public Beat? Previous { get; private set; }
    [JsonIgnore] public bool LastInMeasure { get; private set; }
    [JsonIgnore] public Time RawDuration { get; private set; }

    public void SetNavigation(Voice voice, int index)
    {
        Index = index;
        Voice = voice;
        LastInMeasure = Index == Measure.Voices[Voice.Index].Beats.Count - 1;

        if (Index > 0)
        {
            Previous = Voice.Beats[Index - 1];
        }
        else if (Measure.Previous?.Voices.Count > Voice.Index)
        {
            var prevBeat = Measure.Previous?.Voices[Voice.Index].Beats;
            if (prevBeat != null)
            {
                Previous = prevBeat[^1];
            }
        }

        if (Previous != null)
        {
            Previous.Next = this;
        }


        if (!Part.IsPianoLike) // for piano we dont change the fuckin note order
        {
            Notes = Notes.OrderByDescending(e => e.StringNumber).ToList();
        }

        for (var i = 0; i < Notes.Count; i++)
        {
            Notes[i].SetNavigation(this, i);
        }
    }

    public void Build()
    {
        var rawDuration = new Time(Duration[0], Duration[1]);

        if (Previous?.GraceNote == "onBeat")
        {
            rawDuration -= Previous.MusicalDuration;
        }

        if (Next?.GraceNote == "beforeBeat")
        {
            var nextGraceDuration = new Time(Next.Duration[0], Next.Duration[1]);
            rawDuration -= nextGraceDuration;
        }

        if (GraceNote != null && GraceNote != "onBeat" && GraceNote != "beforeBeat")
            throw new Exception("wtf is this then");

        MusicalDuration = rawDuration;

        IsAccord = Notes.Count > 1;

        RelativeBeatStartTime = Index == 0
            ? new Time()
            : Previous!.RelativeBeatStartTime + Previous.MusicalDuration;

        AbsoluteBeatStartTime = Measure.StartTime + RelativeBeatStartTime;

        Notes.ForEach(e => e.Build());
    }

    public bool Is(string nameplate) => nameplate == $"{this}";

    public override string ToString() => $"B{Index} {Voice}";
}