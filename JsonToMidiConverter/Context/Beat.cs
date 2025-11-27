using System.Diagnostics;
using System.Text.Json.Serialization;

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
    [JsonIgnore] public Nóta[] ReversedNotes { get; set; }
    [JsonIgnore] public byte Numerator => (byte)Duration[0];
    [JsonIgnore] public byte Denominator => (byte)Duration[1];
    [JsonIgnore] public bool IsAccord { get; private set; }
    [JsonIgnore] public string Nameplate => $"{Index}{Measure.Index}{Part.Index}";
    [JsonIgnore] public Beat? Next { get; private set; }
    [JsonIgnore] public Beat? Previous { get; private set; }

    public void SetNavigation(Voice voice, int index)
    {
        Index = index;
        Voice = voice;

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

        for (var i = 0; i < Notes.Length; i++)
        {
            Notes[i].SetNavigation(this, i);
        }
    }

    public void Build()
    {
        ReversedNotes = Notes;

        MusicalDuration = Previous?.GraceNote == "onBeat"
            ? new Time(Duration[0], Duration[1]) - Previous.MusicalDuration
            : new Time(Duration[0], Duration[1]);

        IsAccord = Notes.Length > 1;

        RelativeBeatStartTime = Index == 0
            ? new Time()
            : Previous!.RelativeBeatStartTime + Previous.MusicalDuration;

        AbsoluteBeatStartTime = Measure.StartTime + RelativeBeatStartTime;


        Notes = Notes.OrderBy(e => e.StringNumber).ToArray();

        if (!Part.IsPianoLike) // for piano we dont change the fuckin note order
        {
            //Notes = Notes.Reverse().ToArray();
        }

        for (var i = 0; i < Notes.Length; i++)
        {
            Notes[i].Build(this, i);
        }
    }

    public bool Is(string nameplate) => nameplate == $"{this}";

    public override string ToString() => $"B{Index} {Voice}";
}