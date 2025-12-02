using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Context;
using JsonToMidiConverter.Test;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Note = Melanchall.DryWetMidi.Interaction.Note;

namespace JsonToMidiConverter.Models.Song;

public record Slide(int Steps, Time HoldDuration, Time SlideWindow, Time StepDuration, int Direction, bool IsStepped, int TimeDirection, bool PlayHold);


[DebuggerDisplay("N{Index} B{Beat.Index} V{Voice.Index} M{Measure.Index} P{Part.Index}")]
public sealed partial class Nota
{
    [JsonIgnore] public int Index { get; private set; }
    [JsonIgnore] public Beat Beat { get; private set; }
    [JsonIgnore] public Voice Voice => Beat.Voice;
    [JsonIgnore] public Measure Measure => Voice.Measure;
    [JsonIgnore] public Part Part => Measure.Part;
    [JsonIgnore] public Song Song => Part.Song;
    [JsonIgnore] public int Channel { get; private set; }
    [JsonIgnore] public List<TimedEvent> Events { get; } = new();
    [JsonIgnore] public SevenBitNumber NoteNumber { get; set; }
    [JsonIgnore] public Time ActualDuration { get; private set; }
    [JsonIgnore] public Time RawDuration { get; private set; }
    [JsonIgnore] public bool WillBeTied { get; private set; }
    [JsonIgnore] public Context.Slide Slide { get; private set; }
    //[JsonIgnore] public int? MidiStartEventIndex { get; set; }
    //[JsonIgnore] public int? MidiEndEventIndex { get; set; }
    //[JsonIgnore] public int? MidiOffEventIndex { get; set; }
    //[JsonIgnore] public int? MidiOnEventIndex { get; set; }
    [JsonIgnore] public List<MidiNoteEvent> MidiNoteEvents { get; set; } = [];
    [JsonIgnore] public TieContext? TieDetails { get; private set; }
    [JsonIgnore] public Tie TieType { get; private set; }
    [JsonIgnore] public Time PlayedDuration { get; private set; }
    [JsonIgnore] public Nota? Next { get; private set; }
    [JsonIgnore] public Nota? Previous { get; private set; }
    [JsonIgnore] public bool LastInBeat { get; private set; }
    [JsonIgnore] public Time? TremoloDuration { get; private set; }
    [JsonIgnore] public int PureNoteNumber { get; private set; }

    public void SetNavigation(Beat beat, int index)
    {
        Index = index;
        Beat = beat;
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
        NoteNumber = (GetNoteNumber() + Part.Capo).To7();
        PureNoteNumber = GetNoteNumber(false) + Part.Capo;
        Channel = GetNoteChannel();

        if (Tremolo.Count > 0)
        {
            TremoloDuration = new Time(Tremolo[0], Tremolo[1]);
        }

        Slide = SlideString?.ToSlide() ?? Context.Slide.None;

        RawDuration = Staccato
            ? Beat.MusicalDuration.Clone() / 2
            : Beat.MusicalDuration.Clone();



        ActualDuration = RawDuration.ApplyDots(Beat.Dots);
        WillBeTied = GetWillBeTied();

        if (Tie && !WillBeTied)
        {
            TieDetails = new TieContext(this);
            foreach (var tiedNote in TieDetails.FullChain)
            {
                tiedNote.TieDetails = TieDetails;
                tiedNote.TieType = Models.Song.Tie.InBetween;
            }

            TieDetails.Source.TieType = Models.Song.Tie.Source;
            TieDetails.Destination.TieType = Models.Song.Tie.Destination;
        }


        if (Slide != Context.Slide.None && Tie && WillBeTied)
        {
            Debug.Assert(false);
        }

    }

    public Time GetStrum() => new((long)(1.16 * Part.TempoMap.GetTempoAtTime(Beat.AbsoluteBeatStartTime.Span).BeatsPerMinute) + 1);

    public Time GetStartTime()
    {
        if (Part.IsPianoLike) return Beat.AbsoluteBeatStartTime;
        else return Beat.AbsoluteBeatStartTime + GetStrum() * Index;
    }

    public Time GetEndTime()
    {

        if (Beat.LetRing)
        {
            var nextBeat = Beat.Next;
            while (true)
            {
                if (nextBeat == null)
                {
                    return Beat.AbsoluteBeatStartTime + Beat.MusicalDuration;
                }

                var fretSharedNote = nextBeat.Notes.SingleOrDefault(e => e.StringNumber == StringNumber);
                if (fretSharedNote != null)
                {
                    if (fretSharedNote.Tie && fretSharedNote.NoteNumber == NoteNumber)
                    {
                        return fretSharedNote.GetEndTime();
                    }

                    return fretSharedNote.GetStartTime();
                }

                if (!nextBeat.LetRing)
                {
                    if (nextBeat.Rest)
                    {
                        return nextBeat.AbsoluteBeatStartTime;
                    }
                    else
                    {
                        return nextBeat.AbsoluteBeatStartTime + nextBeat.MusicalDuration;
                    }
                    // TODO: that comment breaks a lot

                }



                nextBeat = nextBeat.Next;
            }
        }
        else if (Dead)
        {
            return GetStartTime() + GetPlayDuration();
        }
        else if (WillBeTied)
        {
            return TieDetails.Destination.GetEndTime();
        }

        return Beat.AbsoluteBeatStartTime + GetPlayDuration();
    }

    public Time GetPlayDuration()
    {
        var nextChannelNote = Beat.Next?.Notes.FirstOrDefault(e => e.StringNumber == StringNumber); // its fguckin first or default because drums ofc
        if (nextChannelNote != null && nextChannelNote.Slide == Context.Slide.Below)
        {
            return ActualDuration - 1920; // TODO: when will this magic number break, i do wonder
        }
        if (Dead)
        {
            var tempo = Part.TempoMap.GetTempoAtTime(Beat.AbsoluteBeatStartTime.Span);
            var ticks = tempo.BeatsPerMinute * 4;
            return new Time((long)ticks);
        }
        //if (Tie) return new Time();
        if (WillBeTied && TieDetails != null)
        {
            return TieDetails.FullDuration;
        }


        return ActualDuration;
    }

    public Nota GetTie()
    {
        if (!Tie) throw new Exception("The note is not tied.");

        var prevNote = Previous;
        while (prevNote != null)
        {
            if (Part.InstrumentId == 1024)
            {
                if (prevNote.NoteNumber == NoteNumber)
                {
                    return prevNote;
                }
            }
            else if (prevNote.Fret == Fret)
            {
                return prevNote;
            }

            prevNote = prevNote.Next;
        }

        throw new Exception("If its a tie, why isnt there a next note");
    }

    public IEnumerable<Nota> GetTies()
    {
        if (!Tie) throw new Exception("The note is not tied.");

        var tieNote = this;
        var q = this;
        var w = 0;
        if (this.ToString() == "N0 B0 V0 M80 P0")
        {

        }

        while (true)
        {
            if (w > 100)
            {
                throw new Exception("very unliekly");
            }
            yield return tieNote;
            if (tieNote.Tie)
            {
                tieNote = tieNote.GetTie();
            }
            else break;
        }
    }

    public string GetName() => $"N{Index} B{Beat.Index} M{Measure.Index} P{Part.Index}";

    public Nota GetForwardTie()
    {
        if (!WillBeTied) throw new Exception();

        var nextBeat = Beat.Next;
        while (true)
        {
            var targetNote = nextBeat.Notes.SingleOrDefault(e => e.Tie && e.IsPitchEqual(this));
            if (targetNote != null)
            {
                return targetNote;
            }
            nextBeat = nextBeat.Next;
        }
    }

    public IEnumerable<Nota> GetForwardTies()
    {
        if (!WillBeTied) throw new Exception();

        yield return this;

        var nextTie = GetForwardTie();

        while (true)
        {
            yield return nextTie;

            if (nextTie.WillBeTied)
            {
                nextTie = nextTie.GetForwardTie();
            }
            else break;
        }
    }

    public Nota GetNextStringSibling()
    {
        var nextBeat = Beat.Next;
        while (true)
        {
            var targetNote = nextBeat.Notes.SingleOrDefault(e => e.StringNumber == StringNumber);
            if (targetNote != null)
            {
                if (targetNote != Next)
                {

                }

                return targetNote;
            }

            nextBeat = nextBeat.Next;
        }
    }

    public Nota GetPreviousStringSibling()
    {
        var nextBeat = Beat.Next;
        while (true)
        {
            var targetNote = nextBeat.Notes.SingleOrDefault(e => e.StringNumber == StringNumber);
            if (targetNote != null)
            {
                return targetNote;
            }

            nextBeat = nextBeat.Next;
        }
    }


    public Slide GetSlide()
    {
        //if (Index > 0) throw new Exception("Works only for lead notes");

        var landingNoteNumber = GetSlideTargetPitch();
        var steps = Math.Abs(landingNoteNumber - NoteNumber) - 1;

        if (Slide == Context.Slide.Below)
        {
            var belowSlideWindow = new Time(1920);
            return new Slide(steps, ActualDuration, belowSlideWindow, belowSlideWindow / steps, -1, true, -1, true);
        }

        var duration = TieDetails?.Destination.ActualDuration ?? ActualDuration;

        if (Tie && steps > 1)
        {
            duration = TieDetails.Source.ActualDuration;
        }

        var direction = Math.Sign(landingNoteNumber - NoteNumber);

        var playHold = !Tie;

        if (steps < 1 || Vibrato)
        {
            var legatoHold = Vibrato ? ActualDuration / 2 : ActualDuration;

            return new Slide(steps, legatoHold, new Time(), new Time(10), direction, false, 1, playHold);
        }

        var defaultSlideWindow = Slide == Context.Slide.Downwards || Slide == Context.Slide.Upwards
            ? 0.75 * duration.Tick
            : Math.Min(steps * 960d, duration.Tick / 2d);

        var vibratoMultiplier = Vibrato ? 1.33333 : 1.0;
        var dotMultiplier = 2 - 1 / Math.Pow(2, Beat.Dots);
        var stepSize = Math.Min(960, defaultSlideWindow / steps) * dotMultiplier * vibratoMultiplier;

        var slideWindow = new Time((long)(stepSize * steps));
        var holdDuration = duration - slideWindow;

        if (Tie)
        {
            holdDuration += TieDetails.Source.ActualDuration;
        }

        return new Slide(steps, holdDuration, slideWindow, new Time((long)stepSize), direction, true, 1, playHold);
    }

    public long GetShiftStepSizeTicks()
    {
        var targetPitch = GetSlideTargetPitch();
        var semitoneDistance = Math.Abs(targetPitch - NoteNumber);

        if ((semitoneDistance == 1 && Slide == Context.Slide.Shift) || Slide == Context.Slide.Legato)
        {
            return ActualDuration.Tick;
        }

        var isSlideOut = Slide == Context.Slide.Downwards || Slide == Context.Slide.Upwards;
        var totalTicks = ActualDuration;
        var maxDuration = isSlideOut
            ? totalTicks * 3 / 4  // 75% Cap
            : totalTicks / 2;       // 50% Cap



        // var idealDuration = new Time(semitoneDistance - 1) * 960;
        var idealDuration = isSlideOut
            ? new Time(semitoneDistance) * 960
            : new Time(semitoneDistance - 1) * 960;

        var finalDuration = idealDuration < maxDuration ? idealDuration : maxDuration;

        long denominator = isSlideOut
            ? semitoneDistance
            : semitoneDistance - 1;

        var oldImpl = (finalDuration / denominator).Tick;



        long numberOfSteps = isSlideOut
            ? semitoneDistance      // slideOut goes through ALL semitones including target
            : semitoneDistance - 1; // normal slide stops before target


        // testing code
        if (Index == 0)
        {
            var slideInfo = GetSlide();
            if (slideInfo.StepDuration.Tick != oldImpl)
            {
                var q = this;
                //Debugger.Break();
            }
        }

        return oldImpl;
    }

    public bool GetWillBeTied()
    {
        var nextBeat = Beat.Next;
        if (nextBeat == null) return false;

        return nextBeat.Notes.Any(e => e.Tie && e.IsPitchEqual(this));
    }

    public bool IsPitchEqual(Nota note) => note.Fret == Fret && (int)note.StringNumber == (int)StringNumber;

    public bool Is(string id) => id == ToString();

    public static readonly IReadOnlyDictionary<double, int> FretHarmonicOffsets = new Dictionary<double, int>
    {
        [2.4] = 36,
        [2.7] = 34,
        [3.2] = 31,
        [3] = 31,
        [4] = 28,
        [5] = 24,
        [7] = 19,
        [9] = 28,
        [12] = 12,
        [19] = 19,
        [24] = 24,
    };

    public int GetNoteNumber(bool withHarmonic = true)
    {
        if (Rest) return 0;

        if (Part.InstrumentId == 1024 || (int)StringNumber == -1)
        {
            return DrumMapping.Mapping.TryGetValue(Fret, out var noteNumber) ? noteNumber.NoteNumber : Fret; // default to Acoustic Bass Drum
        }

        int open = Part.Tuning.Length == 0 ? (int)StringNumber : Part.Tuning[(int)StringNumber];

        if (Harmonic == null || !withHarmonic) return open + Fret;
        var harmonicOffset = FretHarmonicOffsets[HarmonicFret];
        if (Harmonic.Equals("natural", StringComparison.OrdinalIgnoreCase)) return open + harmonicOffset;
        //if (Harmonic.Equals("pinch", StringComparison.OrdinalIgnoreCase)) return open + harmonicOffset - Fret;
        return open + harmonicOffset + Fret;
    }

    public IEnumerable<int> GetEmittedNotes()
    {
        if (!Tie &&
            !TremoloDuration.HasValue &&
            !Slide.ToString().StartsWith("Below") &&
            !Slide.ToString().StartsWith("Above"))
        {
            yield return NoteNumber;
        }

        if (Slide != Context.Slide.None)
        {
            if (Slide == Context.Slide.BelowLegato)
            {
                foreach (var note in GetSlideEmittedNotes(this, Context.Slide.Below))
                    yield return note;

                foreach (var note in GetSlideEmittedNotes(this, Context.Slide.Legato))
                    yield return note;
            }
            else if (Slide == Context.Slide.BelowDownwards)
            {
                foreach (var note in GetSlideEmittedNotes(this, Context.Slide.Below))
                    yield return note;

                foreach (var note in GetSlideEmittedNotes(this, Context.Slide.Downwards))
                    yield return note;
            }
            else if (Slide == Context.Slide.BelowShift)
            {
                foreach (var note in GetSlideEmittedNotes(this, Context.Slide.Below))
                    yield return note;

                foreach (var note in GetSlideEmittedNotes(this, Context.Slide.Shift))
                    yield return note;
            }
            else
            {
                foreach (var note in GetSlideEmittedNotes(this, Slide))
                    yield return note;
            }
        }


        if (TremoloDuration.HasValue)

        {
            var noteDuration = WillBeTied ? TieDetails.FullDuration.Tick : RawDuration.Tick;
            var repeats = noteDuration / TremoloDuration.Value.Tick;
            for (var i = 0; i < repeats; i++)
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

        var q = this;
    }

    private static IEnumerable<int> GetSlideEmittedNotes(Nota note, Context.Slide slide)
    {
        var target = GetSlideTargetPitch(slide, note, out var tagetNote);
        var delta = target - note.Fret;
        var sign = Math.Sign(delta);
        var steps = Math.Abs(target - note.Fret);

        if (slide == Context.Slide.Shift && delta == 0) yield break;

        if (slide == Context.Slide.Shift && delta == 0 && note.WillBeTied && note.TieDetails.Destination.Slide == Context.Slide.None)
        {
            sign = -1;
            steps = 2;
        }

        if (slide == Context.Slide.Shift && delta == 1)
        {
            //steps++;
        }

        if (slide == Context.Slide.Below && delta == 0)
        {
            sign = -1;
            steps = 1;
        }

        if (slide == Context.Slide.Below || slide == Context.Slide.Above)
        {
            for (var i = 1; i < steps + 1; i++)
            {
                yield return note.PureNoteNumber + sign * (steps - i);
            }
        }
        else
        {
            for (var i = 1; i < steps; i++)
            {
                yield return note.PureNoteNumber + sign * i;
            }
        }
    }

    public Nota? GetSlideTargetNote()
    {
        if (Slide == Context.Slide.Shift) return GetNextStringSibling();
        if (Slide == Context.Slide.Downwards) return null;
        if (Slide == Context.Slide.Upwards) return null;
        if (Slide == Context.Slide.Legato) return GetNextStringSibling();
        if (Slide == Context.Slide.Below) return null;

        throw new Exception("what slide");
    }

    public static int GetSlideTargetPitch(Context.Slide slide, Nota note, out Nota? targetNote)
    {
        if (slide == Context.Slide.Shift || slide == Context.Slide.Legato)
        {
            targetNote = note.GetNextStringSibling();
            if (targetNote.NoteNumber == note.NoteNumber && targetNote.Tie)
            {
                targetNote = targetNote.GetNextStringSibling();
            }
            return targetNote.Fret;
        }

        var maxFretSeparation = note.Beat.Notes.Max(e => e.Fret) - note.Beat.Notes.Min(e => e.Fret);
        var moveTogether = maxFretSeparation < 10;

        targetNote = null;

        if (note.Song.SongId == 19 && note.Slide == Context.Slide.Upwards)
        {

        }

        if (slide == Context.Slide.Upwards || slide == Context.Slide.Above)
        {
            // the killin the name: https://www.songsterr.com/a/wsa/rage-against-the-machine-killing-in-the-name-tab-s360t5
            // really wants to go above 24, on ryhtm guitar at measure 46, there are two double-upwards, both goes up 10 steps,
            // including fret 15 on string 4, resulting in fret 25 - both strings go up equally 10 steps each-each
            // but enter sandmen: https://www.songsterr.com/a/wsa/metallica-enter-sandman-tab-s19
            // on the lead guitar at measure 99 after a long tie chain theres a double upwards from 17/16 
            // both note goes up 6 semitones, stopping at 24. if we go till 25 we overflow with one extra note on each note 
            var maxChordFret = note.Beat.Notes.Where(e => e.Slide == note.Slide).Max(e => e.Fret);
            var maxTargetFret = Math.Min(Math.Max(24, maxChordFret), maxChordFret + 10);
            var maxDistance = maxTargetFret - maxChordFret;

            if (slide == Context.Slide.Upwards && maxDistance == 9)
            {
                return note.Fret + 10; // the killin in the name rule or idk
            }

            return note.Fret + maxDistance;
        }
        if (slide == Context.Slide.Downwards || slide == Context.Slide.Below)
        {
            var minChordFret = note.Beat.Notes.Where(e => e.Slide == note.Slide).Min(e => e.Fret);
            var minTargetFret = minChordFret - Math.Min(10, minChordFret);
            var minDistance = minTargetFret - minChordFret;

            if (!moveTogether)
            {
                return note.Fret - Math.Min(10, note.Fret);
            }

            return note.Fret + minDistance;
        }

        throw new Exception("what slide");
    }


    public int GetSlideTargetPitch() => GetSlideTargetPitch(Slide, this, out _);

    public FourBitNumber GetNoteChannel()
    {
        if (Part.InstrumentId == 1024) return 9.To4();
        return (FourBitNumber)StringNumber;
    }

    public override string ToString() => $"N{Index} {Beat}";
}

public enum Tie
{
    None,
    Source,
    Destination,
    InBetween
}

public sealed class TieContext
{
    public Nota Source { get; }
    public Nota Destination { get; }
    public IReadOnlyList<Nota> InBetweenNotes { get; }
    public IReadOnlyList<Nota> FullChain { get; }
    public Time FullDuration { get; }
    public Time FullRawDuration { get; }

    public TieContext(Nota destinationNote)
    {
        if (!destinationNote.Tie || destinationNote.WillBeTied) throw new Exception("no");

        FullChain = destinationNote.GetTies().Reverse().ToList();
        Source = FullChain[0];
        Destination = FullChain[^1];
        InBetweenNotes = FullChain.Skip(1).Take(FullChain.Count - 2).ToList();
        FullDuration = new Time(FullChain.Sum(e => e.ActualDuration.Tick));
        FullDuration = new Time(FullChain.Sum(e => e.RawDuration.Tick));
    }
}