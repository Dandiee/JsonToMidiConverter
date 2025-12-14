using JsonToMidiConverter.Context;
using JsonToMidiConverter.Models.Song.Enums;
using Melanchall.DryWetMidi.MusicTheory;
using System.Diagnostics;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} V{Voice.Index} M{Measure.Index} P{Part.Index} - {Duration.Span}")]
public sealed class Beat : BeatRaw, IMusicalElement<Beat>
{
    public Beat(BeatRaw raw)
    {
        this.Bootstrap(raw);
        Notes = raw.NotesRaw.Select(e => new Nota(e)).ToList();
    }

    private Time? _duration;
    public Time Duration
    {
        get
        {
            if (!_duration.HasValue)
            {
                _duration = new Time(DurationArray.Numerator, DurationArray.Denominator);
            }

            return _duration.Value;
        }
        set
        {
            if (_duration.HasValue)
            {
                Modifications.Add($"Changed from {_duration.Value.Span} to {value.Span}");
            }
            _duration = value;
        }
    }
    public Voice Voice { get; private set; }
    public Measure Measure => Voice.Measure;
    public Song Song => Part.Song;
    public bool LastInMeasure { get; private set; }
    public List<string> Modifications { get; private set; } = [];
    public List<Beat>? BeamGroup { get; set; }
    public Velocity CalculatedVelocity { get; set; }
    public List<Beat>? GradualVelocityGroup { get; set; }
    public List<Nota>? Notes { get; set; }


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
    public Part Part { get; set; }
    public int Index { get; set; }
    public Beat? Next { get; set; }
    public Beat? Previous { get; set; }
    public Time Start { get; set; }
    public Time End { get; set; }
}
