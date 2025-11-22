using System.Diagnostics;
using System.Text.Json.Serialization;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} M{Measure.Index} P{Part.Index}")]
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
    [JsonIgnore] public Nóta[] ReversedNotes { get; set; }
    [JsonIgnore] public byte Numerator => (byte)Duration[0];
    [JsonIgnore] public byte Denominator => (byte)Duration[1];
    [JsonIgnore] public bool IsAccord { get; private set; }
    public void Build(Voice voice, int index)
    {
        ReversedNotes = Notes;
        Index = index;
        Voice = voice;
        MusicalDuration = new Time(Duration[0], Duration[1]);
        IsAccord = Notes.Length > 1;
        var prevBeat = GetPrevious();
        RelativeBeatStartTime = Index == 0
            ? new Time()
            : prevBeat.RelativeBeatStartTime + prevBeat.MusicalDuration;

        AbsoluteBeatStartTime = Measure.StartTime + RelativeBeatStartTime;

        Notes = Notes.OrderBy(e => e.StringNumber).ToArray();

        if (!Part.IsPianoLike) // for piano we dont change the fuckin note order
        {
            Notes = Notes.Reverse().ToArray();
        }

        for (var i = 0; i < Notes.Length; i++)
        {
            Notes[i].Build(this, i);
        }
    }

    public Beat? GetNext()
    {
        if (Index < Measure.Beats.Length - 1)
            return Measure.Beats[Index + 1];

        var nextMeasure = Measure.GetNext();
        if (nextMeasure != null)
            return nextMeasure.Beats[0];

        return null;

    }

    public Beat? GetPrevious()
    {
        if (Index > 0)
            return Measure.Beats[Index - 1];

        var previousMeasure = Measure.GetPrevious();
        if (previousMeasure != null)
            return previousMeasure.Beats[^1];

        return null;
    }


    public long GetMeasureStartDuration(TempoMap tempoMap)
        => Measure.Beats
            .TakeWhile(e => e != this)
            .Sum(e => e.MusicalDuration.Tick);
}