using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Context;
using Melanchall.DryWetMidi.Common;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("N{Index} B{Beat.Index} V{Voice.Index} M{Measure.Index} P{Part.Index}")]
public sealed partial class Nota : MusicalElement<Nota>
{
    [JsonIgnore] public Beat Beat { get; private set; }
    [JsonIgnore] public Voice Voice => Beat.Voice;
    [JsonIgnore] public Measure Measure => Voice.Measure;
    [JsonIgnore] public Song Song => Part.Song;
    [JsonIgnore] public int Channel { get; private set; }
    [JsonIgnore] public SevenBitNumber NoteNumber { get; set; }
    [JsonIgnore] public bool WillBeTied { get; private set; }
    [JsonIgnore] public List<Context.Slide> Slides { get; private set; } = [];
    [JsonIgnore] public List<TimedNoteEvent> MidiNoteEvents { get; set; } = [];
    [JsonIgnore] public TieContext? TieDetails { get; private set; }
    [JsonIgnore] public bool LastInBeat { get; private set; }
    [JsonIgnore] public Time? TremoloDuration { get; private set; }
    [JsonIgnore] public int PureNoteNumber { get; private set; }
    [JsonIgnore] public bool IsHpTarget { get; private set; }

    public void SetNavigation(Beat beat, int index)
    {
        Index = index;
        Beat = beat;
        Part = beat.Part;
        LastInBeat = Index == Beat.Notes.Count - 1;

        var prevBeat = Beat.Previous;
        while (prevBeat != null && Previous == null)
        {
            var prevNote = prevBeat.Notes.FirstOrDefault(n => (int)n.StringNumber == (int)StringNumber);
            if (prevNote != null)
            {
                Previous = prevNote;
                prevNote.Next = this;
            }
            else prevBeat = prevBeat.Previous;
        }
    }

    public void Build()
    {
        Slides = RawSlide?.ToSlides().ToList() ?? [];
        NoteNumber = (this.GetNoteNumber() + Part.Capo).To7();
        PureNoteNumber = this.GetNoteNumber(false) + Part.Capo;
        Channel = this.GetNoteChannel();

        if (Tremolo != null)
        {
            TremoloDuration = new Time(Tremolo.Numerator, Tremolo.Denominator);

        }

        WillBeTied = Next?.Tie ?? false;

        if (Tie && !WillBeTied)
        {
            TieDetails = new TieContext(this);

            foreach (var tiedNote in TieDetails.FullChain)
            {
                tiedNote.TieDetails = TieDetails;
            }
        }

        IsHpTarget = Previous?.Hp == true;
    }

    private static readonly HashSet<Slide> SlidesWhichMakesTheNotePlayEarlierForSomeReason = [Slide.Below, Slide.Above];

    public void SetTimings()
    {
        Start = Slides.Any(e => SlidesWhichMakesTheNotePlayEarlierForSomeReason.Contains(e))
            ? Beat.Start - 1920
            : Beat.Start;

        Start += Part.IsPianoLike ? new Time() : new Time(100 * Index);

        End = GetEndTime();

        if (Next?.Slides.Contains(Slide.Below) == true)
        {
            //End -= 1920;
        }

        Duration = End - Start;
    }

    private Time GetEndTime()
    {
        if (Dead) return Start + 400;

        if (Staccato)
        {
            var totalTiedDuration = (TieDetails?.Destination ?? this).Beat.End - Start;
            return Start + totalTiedDuration / 2;
        }

        if (!Beat.LetRing && !WillBeTied)
            return Next?.Slides.IsBefore() == true
                ? Beat.End
                : Beat.End;

        var tieEnd = (TieDetails?.Destination ?? this).Beat.Notes.First(e =>
            Part.InstrumentId == 1024
                ? DrumMapping.Mapping[e.Fret].NoteNumber == DrumMapping.Mapping[Fret].NoteNumber
                : e.StringNumber == StringNumber);


        if (!tieEnd.Beat.LetRing || tieEnd.Bend != null)
        {
            return tieEnd.Beat.Next?.Notes.Any(e => e.StringNumber == StringNumber && e.Slides.IsBefore()) == true
                ? tieEnd.Beat.End
                : tieEnd.Beat.End;
        }

        foreach (var nextBeat in tieEnd.Beat.Forward().Skip(1))
        {
            if (nextBeat.Notes.Any(e => e.StringNumber == StringNumber))
            {
                return nextBeat.Start;
            }

            if (nextBeat.Notes.Any(e => e.Slides.Contains(Slide.Below) || e.Slides.Contains(Slide.Above)))
            {
                continue;
            }

            if (!nextBeat.LetRing)
            {
                return nextBeat.Rest
                    ? nextBeat.Start
                    : nextBeat.End;
            }



            if (nextBeat.Measure.Index > Measure.Index + 20)
            {
                //return tieEnd.Measure.End;
            }
        }

        return Part.Measures[^1].Voices[Voice.Index].Beats[^1].End;
    }


    public IEnumerable<int> GetEmittedNotes()
    {
        if (!Tie &&
            !TremoloDuration.HasValue &&
            !Slides.Contains(Context.Slide.Below) &&
            !Slides.Contains(Context.Slide.Above))
        {
            yield return NoteNumber;
        }

        foreach (var slide in Slides)
        {
            foreach (var note in GetSlideEmittedNotes(this, slide))
            {
                yield return note;
            }
        }

        if (TremoloDuration.HasValue)
        {
            var noteDuration = WillBeTied ? TieDetails.Destination.End.Tick - TieDetails.Source.Start.Tick : Duration.Tick;
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

    private static IEnumerable<int> GetSlideEmittedNotes(Nota note, Context.Slide slide)
    {
        var target = GetSlideTargetPitch(slide, note);
        var delta = target - note.Fret;
        var sign = Math.Sign(delta);
        var steps = Math.Abs(target - note.Fret);

        if (steps > 10)
        {

        }

        if (slide == Context.Slide.Shift && delta == 0) yield break;

        if (slide == Context.Slide.Shift && delta == 0 && note.WillBeTied && note.TieDetails.Destination.Slides.Count == 0)
        {
            sign = -1;
            steps = 2;
        }

        if (slide == Context.Slide.Below && delta == 0)
        {
            sign = -1;
            steps = 1;
        }

        if (slide == Context.Slide.Below || slide == Context.Slide.Above)
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


    public static int GetSlideTargetPitch(Context.Slide slide, Nota note)
    {
        if (slide == Context.Slide.Shift || slide == Context.Slide.Legato)
        {
            var targetNote = note.Next;
            if (targetNote.NoteNumber == note.NoteNumber && targetNote.Tie)
            {
                targetNote = targetNote.Next;
            }
            return targetNote.Fret;
        }

        var affectedStrings = note.Beat.Notes
            .Where(e => (slide != Slide.Below && slide != Slide.Downwards) || e.Fret > 0)
            .ToList();

        if (affectedStrings.Count == 0) return note.Fret;

        var maxFretSeparation = affectedStrings.Max(e => e.Fret) - affectedStrings.Min(e => e.Fret);
        var moveTogether = maxFretSeparation < 10;

        if (slide == Context.Slide.Upwards || slide == Context.Slide.Above)
        {
            // the killin the name: https://www.songsterr.com/a/wsa/rage-against-the-machine-killing-in-the-name-tab-s360t5
            // really wants to go above 24, on ryhtm guitar at measure 46, there are two double-upwards, both goes up 10 steps,
            // including fret 15 on string 4, resulting in fret 25 - both strings go up equally 10 steps each-each
            // but enter sandmen: https://www.songsterr.com/a/wsa/metallica-enter-sandman-tab-s19
            // on the lead guitar at measure 99 after a long tie chain theres a float upwards from 17/16 
            // both note goes up 6 semitones, stopping at 24. if we go till 25 we overflow with one extra note on each note 
            var maxChordFret = note.Beat.Notes.Where(e => e.Slides.Contains(slide)).Max(e => e.Fret);
            var maxTargetFret = Math.Min(Math.Max(24, maxChordFret), maxChordFret + 10);
            var maxDistance = maxTargetFret - maxChordFret;

            if (maxDistance == 9)
            {
                return note.Fret + 10; // the killin in the name rule or idk
            }

            return note.Fret + maxDistance;
        }
        if (slide == Context.Slide.Downwards || slide == Context.Slide.Below)
        {
            if (note.Fret == 0) return 0;

            var minChordFret = note.Beat.Notes
                .Where(e => e.Slides.Contains(slide))
                .Where(e => e.Fret != 0)
                .Min(e => e.Fret);

            var minTargetFret = minChordFret - Math.Min(10, minChordFret);
            var minDistance = minTargetFret - minChordFret;

            if (!moveTogether)
            {
                return Math.Max(0, note.Fret - Math.Min(10, note.Fret));
            }

            return Math.Max(0, note.Fret + minDistance);
        }

        throw new Exception("what slide");
    }



    public override string ToString() => $"N{Index} {Beat}";





}

public sealed class TieContext
{
    public Nota Source { get; }
    public Nota Destination { get; }
    public IReadOnlyList<Nota> FullChain { get; }
    //public Time FullDuration { get; }

    public TieContext(Nota destinationNote)
    {
        if (!destinationNote.Tie || destinationNote.WillBeTied) throw new Exception("no");

        var chain = new List<Nota> { destinationNote };

        while (chain[^1].Previous != null && (chain[^1].Tie))
        {
            chain.Add(chain[^1].Previous!);
        }

        chain.Reverse();

        FullChain = chain;
        Source = FullChain[0];
        Destination = FullChain[^1];
    }
}
