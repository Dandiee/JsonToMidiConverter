using System.Diagnostics;
using Dani.Data.Models.Enums;
using Dani.Data.Models.Parts;

using DataBeat = Dani.Data.Models.Parts.Beat;

namespace Dani.Converter.Models;

[DebuggerDisplay("B{Index} V{Voice.Index} M{Voice.Measure!.Index} P{Voice.Measure!.Part.Index} - {Duration.Span!}")]
public sealed class Beat : MusicalElement<Beat, Voice>
{
    public Voice Voice => Parent;
    public List<Nota> Notes { get; set; }

    public Velocity CalculatedVelocity { get; set; }
    public GraceNote GraceNote { get; }
    public Spanner BeamSpan { get; }
    public Velocity Velocity { get; }
    public GradualVelocity GradualVelocity { get; }
    public Technique Technique { get; }
    public bool Rest { get; }
    public bool LetRing { get; }
    public Bend? Tremolo { get; }

    public List<Beat>? BeamGroup { get; set; }
    public List<Beat>? GradualVelocityGroup { get; set; }

    public Beat(Voice voice, DataBeat data, int index)
     : base(voice.Part, voice, index)
    {
        Duration = data.Duration.ToTime();
        GraceNote = data.GraceNote;
        BeamSpan = data.BeamSpan;
        Velocity = data.Velocity;
        GradualVelocity = data.GradualVelocity;
        Rest = data.Rest;
        LetRing = data.LetRing;
        Technique = data.Technique;
        Tremolo = data.Tremolo;

        var orderedNotes = data.Notes
            //.Where(e => !e.Rest)
            .OrderByDescending(e => e.DoubledString)
            .ToList();

        Notes = new List<Nota>(orderedNotes.Count);
        for (var i = 0; i < orderedNotes.Count; i++)
        {
            Notes.Add(new Nota(this, orderedNotes[i], i));
        }
    }

    protected override Beat? GetPrevious(object? state = null)
    {
        if (Index > 0)
        {
            return Voice.Beats[Index - 1];
        }

        if (Voice.Measure.Previous?.Voices.Count > Voice.Index)
        {
            var prevBeat = Voice.Measure.Previous?.Voices[Voice.Index].Beats;
            if (prevBeat != null)
            {
                return prevBeat[^1];
            }
        }

        return null;
    }

    public void SetTimes()
    {
        Start = Previous?.End ?? new Time();

        if (Voice.Index > 0 && Previous == null)
        {
            Start = Voice.Measure.Start;
        }

        End = Start + Duration;
    }

    public override string ToString() => $"B{Index} {Voice}";
}
