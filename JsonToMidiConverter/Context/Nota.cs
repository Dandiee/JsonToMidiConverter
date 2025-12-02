using System.Diagnostics;
using System.Text.Json.Serialization;
using Melanchall.DryWetMidi.Common;

namespace JsonToMidiConverter.Models.Song;

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
    [JsonIgnore] public SevenBitNumber NoteNumber { get; set; }
    [JsonIgnore] public Time ActualDuration { get; private set; }
    [JsonIgnore] public Time RawDuration { get; private set; }
    [JsonIgnore] public bool WillBeTied { get; private set; }
    [JsonIgnore] public List<Context.Slide> Slides { get; private set; } = [];
    [JsonIgnore] public List<TimedNoteEvent> MidiNoteEvents { get; set; } = [];
    [JsonIgnore] public TieContext? TieDetails { get; private set; }
    [JsonIgnore] public Tie TieType { get; private set; }
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
        Slides = SlideString?.ToSlides().ToList() ?? [];
        NoteNumber = (GetNoteNumber() + Part.Capo).To7();
        PureNoteNumber = GetNoteNumber(false) + Part.Capo;
        Channel = GetNoteChannel();

        if (Tremolo.Count > 0)
        {
            TremoloDuration = new Time(Tremolo[0], Tremolo[1]);
        }

        RawDuration = Staccato
            ? Beat.MusicalDuration.Clone() / 2
            : Beat.MusicalDuration.Clone();

        ActualDuration = RawDuration.ApplyDots(Beat.Dots);
        WillBeTied = Next?.Tie ?? false;

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
            if (w++ > 100)
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

        if (slide == Context.Slide.Shift && delta == 0 && note.WillBeTied && note.TieDetails.Destination.Slides.Count == 0)
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


    public static int GetSlideTargetPitch(Context.Slide slide, Nota note, out Nota? targetNote)
    {
        if (slide == Context.Slide.Shift || slide == Context.Slide.Legato)
        {
            targetNote = note.Next;
            if (targetNote.NoteNumber == note.NoteNumber && targetNote.Tie)
            {
                targetNote = targetNote.Next;
            }
            return targetNote.Fret;
        }

        var maxFretSeparation = note.Beat.Notes.Max(e => e.Fret) - note.Beat.Notes.Min(e => e.Fret);
        var moveTogether = maxFretSeparation < 10;

        targetNote = null;

        if (slide == Context.Slide.Upwards || slide == Context.Slide.Above)
        {
            // the killin the name: https://www.songsterr.com/a/wsa/rage-against-the-machine-killing-in-the-name-tab-s360t5
            // really wants to go above 24, on ryhtm guitar at measure 46, there are two double-upwards, both goes up 10 steps,
            // including fret 15 on string 4, resulting in fret 25 - both strings go up equally 10 steps each-each
            // but enter sandmen: https://www.songsterr.com/a/wsa/metallica-enter-sandman-tab-s19
            // on the lead guitar at measure 99 after a long tie chain theres a double upwards from 17/16 
            // both note goes up 6 semitones, stopping at 24. if we go till 25 we overflow with one extra note on each note 
            var maxChordFret = note.Beat.Notes.Where(e => e.Slides.Contains(slide)).Max(e => e.Fret);
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
            var minChordFret = note.Beat.Notes.Where(e => e.Slides.Contains(slide)).Min(e => e.Fret);
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