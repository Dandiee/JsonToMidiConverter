using JsonToMidiConverter.Context;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} V{Voice.Index} M{Measure.Index} P{Part.Index}")]
public sealed partial class Beat : MusicalElement<Beat>
{
    [JsonIgnore] public Voice Voice { get; private set; }
    [JsonIgnore] public Measure Measure => Voice.Measure;
    [JsonIgnore] public Song Song => Part.Song;
    [JsonIgnore] public bool LastInMeasure { get; private set; }
    private Time? _duration;
    [JsonIgnore]
    public override Time Duration
    {
        get
        {
            if (!_duration.HasValue)
            {
                _duration = new Time(DurationArray[0], DurationArray[1]);
            }

            return _duration.Value;
        }
        set => _duration = value;
    }

    public void SetNavigation(Voice voice, int index)
    {
        Index = index;
        Voice = voice;
        LastInMeasure = Index == Measure.Voices[Voice.Index].Beats.Count - 1;
        Part = voice.Part;

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


        if (!Part.IsPianoLike)
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
        Notes.ForEach(e => e.Build());
    }

    public void SetTimes()
    {
        Start = Previous?.End ?? new Time();

        if (Voice.Index > 0 && Previous == null)
        {
            Start = Measure.Start;
        }

        End = Start + Duration;
    }

    public override string ToString() => $"B{Index} {Voice}";
}