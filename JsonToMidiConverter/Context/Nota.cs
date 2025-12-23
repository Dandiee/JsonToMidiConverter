using Dani.Data.Models.Enums;
using Dani.Data.Models.Parts;
using JsonToMidiConverter.Context;
using Melanchall.DryWetMidi.Common;
using System.Diagnostics;
using Dani.Data.Factories;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("N{Index} B{Beat.Index} V{Beat.Voice.Index} M{Beat.Voice.Measure.Index} P{Part.Index}")]
public sealed class Nota : MusicalElement<Nota, Beat>
{
    public Beat Beat => Parent;
    public int Channel { get; }
    public SevenBitNumber NoteNumber { get; }
    public SlideFlags Slides { get; }
    public Time? TremoloDuration { get; }
    public int PureNoteNumber { get; }
    public double StringNumber { get; }
    public bool Dead { get; }
    public sbyte Fret { get; }
    public bool Staccato { get; }
    public bool Tie { get; }
    public bool Rest { get; }
    public bool Ghost { get; }
    public Accent Accentuated { get; }
    public Legato Legato { get;}
    public Harmonic Harmonic { get; }
    public float HarmonicFret { get; }
    public Bend? Bend { get; }

    public List<TimedNoteEvent> MidiNoteEvents { get; set; } = [];
    public TieContext? TieDetails { get; private set; }
    public bool IsHpTarget { get; private set; }
    public bool WillBeTied { get; set; }

    public Nota(Beat beat, Note data, int index)
     : base(beat.Part, beat, index, data.DoubledString)
    {
        var capo = Beat.Voice.Measure.Part.Capo;

        Fret = data.Fret;
        Slides = data.Slides;
        Ghost = data.Ghost;
        Harmonic = data.Harmonic;
        Accentuated = data.Accentuated;
        StringNumber = data.DoubledString / 2.0;
        NoteNumber = (this.GetNoteNumber() + capo).To7();
        PureNoteNumber = this.GetNoteNumber(false) + capo;
        Channel = this.GetNoteChannel();
        TremoloDuration = data.Tremolo.IsZero() ? null : data.Tremolo.ToTime();
        Legato = data.Legato;
        Rest = data.Rest;
        Bend = data.Bend;
        HarmonicFret = NoteFactory.HarmonicFretTable[data.HarmonicFretIndex];
        IsHpTarget = Previous?.Legato == Legato.HammerPull;
        Dead = data.Dead;
        Staccato = data.Staccato;
        Tie = data.Tie;

        if (Tie && Previous != null)
        {
            Previous.WillBeTied = true;
        }
    }

    public void SecondPass()
    {
        if (Tie && !WillBeTied)
        {
            TieDetails = new TieContext(this);

            foreach (var tiedNote in TieDetails.FullChain)
            {
                tiedNote.TieDetails = TieDetails;
            }
        }
    }

    protected override Nota? GetPrevious(object? state = null)
    {
        var stringNUmber = (sbyte)state! / 2.0d;

        var prevBeat = Beat.Previous;
        while (prevBeat != null && Previous == null && !prevBeat.Rest)
        {
            var prevNote = prevBeat.Notes.FirstOrDefault(n => (int)n.StringNumber == stringNUmber);
            if (prevNote != null)
            {
                if (prevNote.Rest) return null;

                return prevNote;
            }

            prevBeat = prevBeat.Previous;
        }

        return null;
    }

    public void SetTimings()
    {
        Start = Slides.Has(SlideFlags.FromAbove) || Slides.Has(SlideFlags.FromBelow)
            ? Beat.Start - 1920
            : Beat.Start;

        Start += Beat.Voice.Measure.Part.IsPianoLike ? new Time() : new Time(100 * Index);

        End = GetEndTime();

        if (Next?.Slides.Has(SlideFlags.FromBelow) == true)
        {
            //End -= 1920;
        }

        Duration = End - Start;
    }

    private Time GetEndTime()
    {
        if (Rest) return Beat.End;

        if (Dead) return Start + 400;

        if (Staccato)
        {
            var totalTiedDuration = (TieDetails?.Destination ?? this).Beat.End - Start;
            return Start + totalTiedDuration / 2;
        }

        if (!Beat.LetRing && !WillBeTied)
            return Next?.Slides.Has(SlideFlags.FromBelow) ==  true
                ? Beat.End
                : Beat.End;

        var tieEnd = (TieDetails?.Destination ?? this).Beat.Notes.First(e =>
            Beat.Voice.Measure.Part.IsDrum
                ? DrumMapping.Mapping[e.Fret].NoteNumber == DrumMapping.Mapping[Fret].NoteNumber
                : e.StringNumber == StringNumber);


        if (!tieEnd.Beat.LetRing || tieEnd.Bend != null)
        {
            return tieEnd.Beat.Next?.Notes.Any(e => e.StringNumber == StringNumber && e.Slides.Has(SlideFlags.FromBelow)) == true
                ? tieEnd.Beat.End
                : tieEnd.Beat.End;
        }

        foreach (var nextBeat in tieEnd.Beat.Forward().Skip(1))
        {
            if (nextBeat.Notes.Any(e => e.StringNumber == StringNumber))
            {
                return nextBeat.Start;
            }

            if (nextBeat.Notes.Any(e => e.Slides.Has(SlideFlags.FromBelow) || e.Slides.Has(SlideFlags.FromAbove)))
            {
                continue;
            }

            if (!nextBeat.LetRing)
            {
                return nextBeat.Rest
                    ? nextBeat.Start
                    : nextBeat.End;
            }



            if (nextBeat.Voice.Measure.Index > Beat.Voice.Measure.Index + 20)
            {
                //return tieEnd.Measure.End;
            }
        }

        return Beat.Voice.Measure.Part.Measures[^1].Voices[Beat.Voice.Index].Beats[^1].End;
    }


    public IEnumerable<int> GetEmittedNotes()
    {
        if (!Tie &&
            !TremoloDuration.HasValue &&
            !Slides.Has(SlideFlags.FromBelow) &&
            !Slides.Has(SlideFlags.FromAbove))
        {
            yield return NoteNumber;
        }

        foreach (var slide in Slides.GetUniques())
        {
            foreach (var note in GetSlideEmittedNotes(this, slide))
            {
                yield return note;
            }
        }

        if (TremoloDuration.HasValue)
        {
            var noteDuration = WillBeTied 
                ? TieDetails.Destination.End.Tick - TieDetails.Source.Start.Tick
                : Duration.Tick;
            var integerRepeats = noteDuration / TremoloDuration.Value.Tick;
            var leftover = noteDuration / (float)TremoloDuration.Value.Tick - integerRepeats;
            if (leftover > 0.5)
            {
                integerRepeats++;
            }

            //var repeats = noteDuration / TremoloDuration.Value.Tick;
            for (var i = 0; i < integerRepeats; i++)
            {
                if (Tie && i == 0) continue;
                if (Dead)
                {
                    if (!Tie && i == 0) yield return NoteNumber;

                    continue;
                }

                yield return NoteNumber;
            }
        }
    }

    private static IEnumerable<int> GetSlideEmittedNotes(Nota note, SlideFlags slide)
    {
        var target = GetSlideTargetPitch(slide, note);
        var delta = target - note.Fret;
        var sign = Math.Sign(delta);
        var steps = Math.Abs(target - note.Fret);

        if (steps > 10)
        {

        }

        if (slide == SlideFlags.Shift && delta == 0) yield break;
        if (slide == SlideFlags.Shift && delta == 0 && note.WillBeTied && note.TieDetails.Destination.Slides == SlideFlags.None)
        {
            sign = -1;
            steps = 2;
        }

        if (slide == SlideFlags.FromBelow && delta == 0)
        {
            sign = -1;
            steps = 1;
        }

        if (slide == SlideFlags.FromBelow || slide == SlideFlags.FromAbove)
        {
            for (var i = 1; i < steps; i++)
            {
                yield return note.PureNoteNumber + sign * (steps - i);
            }

            yield return note.NoteNumber;
        }
        else
        {
            for (var i = 1; i < steps; i++)
            {
                yield return note.PureNoteNumber + sign * i;
            }
        }
    }


    public static sbyte GetSlideTargetPitch(SlideFlags slide, Nota note)
    {
        if (slide == SlideFlags.Shift || slide == SlideFlags.Legato)
        {
            var targetNote = note.Next;
            if (targetNote == null)
            {
                return (sbyte)Math.Max(0, note.Fret - 2);
            }

            if (targetNote.NoteNumber == note.NoteNumber && targetNote.Tie)
            {
                targetNote = targetNote.Next ?? targetNote;
            }
            return targetNote.Fret;
        }

        var affectedStrings = note.Beat.Notes
            .Where(e => (slide != SlideFlags.FromBelow && slide != SlideFlags.Downwards) || e.Fret > 0)
            .ToList();

        if (affectedStrings.Count == 0) return note.Fret;

        var maxFretSeparation = affectedStrings.Max(e => e.Fret) - affectedStrings.Min(e => e.Fret);
        var moveTogether = maxFretSeparation < 10;

        if (slide == SlideFlags.Upwards || slide == SlideFlags.FromAbove)
        {
            // the killin the name: https://www.songsterr.com/a/wsa/rage-against-the-machine-killing-in-the-name-tab-s360t5
            // really wants to go above 24, on ryhtm guitar at measure 46, there are two double-upwards, both goes up 10 steps,
            // including fret 15 on string 4, resulting in fret 25 - both strings go up equally 10 steps each-each
            // but enter sandmen: https://www.songsterr.com/a/wsa/metallica-enter-sandman-tab-s19
            // on the lead guitar at measure 99 after a long tie chain theres a float upwards from 17/16 
            // both note goes up 6 semitones, stopping at 24. if we go till 25 we overflow with one extra note on each note 
            var maxChordFret = note.Beat.Notes.Where(e => e.Slides.Has(slide)).Max(e => e.Fret);
            var maxTargetFret = Math.Min(Math.Max((sbyte)24, maxChordFret), maxChordFret + 10);
            var maxDistance = maxTargetFret - maxChordFret;

            if (maxDistance == 9)
            {
                return (sbyte)(note.Fret + 10); // the killin in the name rule or idk
            }

            return (sbyte)(note.Fret + maxDistance);
        }
        if (slide == SlideFlags.Downwards || slide == SlideFlags.FromBelow)
        {
            if (note.Fret == 0) return 0;

            var minChordFret = note.Beat.Notes
                .Where(e => e.Slides.Has(slide))
                .Where(e => e.Fret != 0)
                .Min(e => e.Fret);

            var minTargetFret = minChordFret - Math.Min((sbyte)10, minChordFret);
            var minDistance = minTargetFret - minChordFret;

            if (!moveTogether)
            {
                return (sbyte)Math.Max(0, note.Fret - Math.Min((sbyte)10, note.Fret));
            }

            return (sbyte)Math.Max(0, note.Fret + minDistance);
        }

        throw new Exception("what slide");
    }

    public override string ToString() => $"N{Index} {Beat}";
}