using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Context;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Note = Melanchall.DryWetMidi.Interaction.Note;

namespace JsonToMidiConverter.Models.Song;

public record SlideInfo(int Steps, Time HoldDuration, Time SlideWindow, Time StepDuration);


[DebuggerDisplay("N{Index} B{Beat.Index} M{Measure.Index} P{Part.Index} STR{StringNumber}/FRT{Fret} NN{NoteNumber}")]
public sealed partial class Nóta
{
    [JsonIgnore] public int Index { get; private set; }
    [JsonIgnore] public Beat Beat { get; private set; }
    [JsonIgnore] public Voice Voice => Beat.Voice;
    [JsonIgnore] public Measure Measure => Voice.Measure;
    [JsonIgnore] public Part Part => Measure.Part;
    [JsonIgnore] public Song Song => Part.Song;
    [JsonIgnore] public FourBitNumber Channel { get; private set; }
    [JsonIgnore] public List<TimedEvent> Events { get; } = new();
    [JsonIgnore] public SevenBitNumber NoteNumber { get; set; }
    [JsonIgnore] public Time ActualDuration { get; private set; }
    [JsonIgnore] public Time RawDuration { get; private set; }
    [JsonIgnore] public bool WillBeTied { get; private set; }
    [JsonIgnore] public Slide Slide { get; private set; }
    [JsonIgnore] public Slide OriginalSlide { get; private set; }
    [JsonIgnore] public Queue<(MidiEvent Event, Time Time)> PendingEvents { get; private set; } = new();
    [JsonIgnore] public int? MidiEventIndex { get; set; }
    [JsonIgnore] public int? MidiEventCount { get; set; }

    [JsonIgnore] public TieContext? TieDetails { get; private set; }
    [JsonIgnore] public Tie TieType { get; private set; }


    public void Build(Beat beat, int index)
    {
        Index = index;
        Beat = beat;
        NoteNumber = GetNoteNumber().To7();
        Channel = GetNoteChannel();

        Slide = SlideString?.ToSlide() ?? Slide.None;

        RawDuration = Staccato
            ? beat.MusicalDuration.Clone() / 2
            : beat.MusicalDuration.Clone();

        var prevBeat = Beat.GetPrevious();
        ActualDuration = prevBeat?.GraceNote == "onBeat"
            ? RawDuration - prevBeat.MusicalDuration
            : RawDuration;

        OriginalSlide = Slide;

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
    }

    public Nóta GetNext()
    {
        var nextBeat = Beat.GetNext();

        return nextBeat.Notes[0];

        return null;
    }

    public Nóta GetTie()
    {
        if (!Tie) throw new Exception("The note is not tied.");

        var measure = Measure;

        while (true)
        {
            var beatStartIndex = measure == Measure
                ? Beat.Index - 1
                : measure.Beats.Length - 1;

            for (var b = beatStartIndex; b > -1; b--)
            {
                var beat = measure.Beats[b];
                foreach (var note in beat.Notes)
                {
                    if (note.IsPitchEqual(this))
                    {
                        return note;
                    }
                }
            }

            measure = Part.Measures[measure.Index - 1];
        }
    }

    public IEnumerable<Nóta> GetTies()
    {
        if (!Tie) throw new Exception("The note is not tied.");

        var tieNote = this;

        while (true)
        {
            yield return tieNote;
            if (tieNote.Tie)
            {
                tieNote = tieNote.GetTie();
            }
            else break;
        }
    }

    public string GetName() => $"N{Index} B{Beat.Index} M{Measure.Index} P{Part.Index}";

    public Nóta GetForwardTie()
    {
        if (!WillBeTied) throw new Exception();

        var nextBeat = Beat.GetNext();
        while (true)
        {
            var targetNote = nextBeat.Notes.SingleOrDefault(e => e.Tie && e.IsPitchEqual(this));
            if (targetNote != null)
            {
                return targetNote;
            }
            nextBeat = nextBeat.GetNext();
        }
    }

    public IEnumerable<Nóta> GetForwardTies()
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

    public Nóta GetSlideTarget()
    {
        if (Slide == Slide.None) throw new Exception("The not is not a slide.");

        var nextBeat = Beat.GetNext();
        while (true)
        {
            var targetNote = nextBeat.Notes.SingleOrDefault(e => e.StringNumber == StringNumber);
            if (targetNote != null)
            {
                return targetNote;
            }

            nextBeat = nextBeat.GetNext();
        }
    }

    
    public SlideInfo GetSlideInfo()
    {
        if (Index > 0) throw new Exception("Works only for lead notes");

        var landingNoteNumber = GetSlideTargetPitch();

        var steps = Math.Abs(landingNoteNumber - NoteNumber) - 1;
        var duration = TieDetails?.Destination.ActualDuration ?? ActualDuration;

        var defaultSlideWindow = Slide == Slide.Downwards || Slide == Slide.Upwards
            ? 0.75 * duration.Tick
            : Math.Min(steps * 960d, duration.Tick / 2d);

        var vibratoMultiplier = Vibrato ? 1.33333 : 1.0;
        var dotMultiplier = (2 - (1 / Math.Pow(2, Beat.Dots)));
        var stepSize = Math.Min(960, defaultSlideWindow / steps) * dotMultiplier * vibratoMultiplier;

        var slideWindow = new Time((long)(stepSize * steps));
        var holdDuration = duration - slideWindow;

        return new SlideInfo(steps, holdDuration, slideWindow, new Time((long)stepSize));
    }

    public long GetShiftStepSizeTicks()
    {
        var targetPitch = GetSlideTargetPitch();
        var semitoneDistance = Math.Abs(targetPitch - NoteNumber);

        if ((semitoneDistance == 1 && Slide == Slide.Shift) || Slide == Slide.Legato)
        {
            return ActualDuration.Tick;
        }

        var isSlideOut = Slide == Slide.Downwards || Slide == Slide.Upwards;
        var totalTicks = ActualDuration;
        var maxDuration = isSlideOut
            ? (totalTicks * 3) / 4  // 75% Cap
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
            var slideInfo = GetSlideInfo();
            if (slideInfo.StepDuration.Tick != oldImpl)
            {
                var q = this;
                Debugger.Break();
            }
        }

        return oldImpl;
    }

    public bool GetWillBeTied()
    {
        var nextBeat = Beat.GetNext();
        if (nextBeat == null) return false;

        return nextBeat.Notes.Any(e => e.Tie && e.IsPitchEqual(this));
    }

    public bool IsInInbetweenTie()
    {
        if (!Tie) return false;

        for (var m = Measure.Index; m < Part.Measures.Length; m++)
        {
            var measure = Part.Measures[m];
            var beatStartIndex = measure == Measure
                ? Beat.Index + 1
                : 0;

            for (var b = beatStartIndex; b < measure.Beats.Length; b++)
            {
                var beat = measure.Beats[b];
                foreach (var note in beat.Notes)
                {
                    if ((int)note.StringNumber == (int)StringNumber)
                    {
                        return note.Tie && note.Fret == Fret;
                    }
                }
            }
        }

        return false;
    }

    public bool IsPitchEqual(Nóta note) => note.Fret == Fret && (int)note.StringNumber == (int)StringNumber;


    public bool Is(int value) => value == int.Parse($"{Index}{Beat.Index}{Measure.Index}{Part.Index}");


    public void Is(int noteIndex, int beatIndex, int measureIndex, int? partIndex = null)
    {
        if (Index == noteIndex && Beat.Index == beatIndex && Measure.Index == measureIndex &&
            (!partIndex.HasValue || Part.Index == partIndex.Value)) Debugger.Break();
    }

    public int GetNoteNumber()
    {
        if (Part.InstrumentId == 1024 || (int)StringNumber == -1)
        {
            if (Fret == 51) return 59; // nirvana, M5, P6, N1, N0
            if (Fret == 98 && StringNumber == -0.5) return 57;
            if (Fret == 85 && StringNumber == -1.5) return 76;
            if (Fret == 92 && StringNumber == -0.5) return 46;
            return Fret;
        }

        int openStringPitch = Part.Tuning.Length == 0
            ? (int)StringNumber // Fallback
            : Part.Tuning[(int)StringNumber];

        if (Harmonic == "natural")
        {
            switch (Fret) // Or note.harmonicFret
            {
                case 12: return (openStringPitch + 12);
                case 7: return (openStringPitch + 19);
                case 5: return (openStringPitch + 24);
                case 4: return (openStringPitch + 28);
                case 9: return (openStringPitch + 28); // 9th fret harmonic is same as 4th
                case 3: return (openStringPitch + 31); // 3rd fret is +2 Octaves + 5th
            }
        }

        if (Harmonic == "artificial")
        {
            switch ((int)HarmonicFret)
            {
                case 10: return openStringPitch + Fret + 12;
                case 5: return openStringPitch + Fret + 24;
            }
        }

        var q = this;
        //if (Fret == 12) return openStringPitch;
        var w = HarmonicFret;
        // 4. STANDARD FRETTED NOTE

        var randomOffset = Part.InstrumentId == 67
            ? 10
            : 0;

        randomOffset = 0;
        // 55 a target
        if (Part.InstrumentId == 30)
        {
            //if (Fret == 12 && StringNumber == 4) return 45;
        }

        if (Part.InstrumentId == 27)
        {
            return openStringPitch + Fret + 0;
        }

        return (openStringPitch + Fret + (int)HarmonicFret) + randomOffset;
    }

    public SevenBitNumber GetSlideTargetPitch()
    {
        if (Slide == Slide.Shift) return GetSlideTarget().NoteNumber;
        if (Slide == Slide.Downwards) return (NoteNumber - Math.Min(10, Fret)).To7();
        if (Slide == Slide.Upwards) return (NoteNumber + 10).To7();
        if (Slide == Slide.Legato) return GetSlideTarget().NoteNumber;

        throw new Exception("what slide");
    }

    public FourBitNumber GetNoteChannel()
    {
        if (Part.InstrumentId == 71 || Part.InstrumentId == 68
                                    || Part.InstrumentId == 27
                                    || Part.InstrumentId == 30
                                    || Part.InstrumentId == 40
                                    || Part.InstrumentId == 29
                                    || Part.InstrumentId == 37
                                    || Part.InstrumentId == 67
                                    || Part.InstrumentId == 26
                                    || Part.InstrumentId == 12
                                    || Part.InstrumentId == 81)
        {
            return (FourBitNumber)StringNumber;
        }


        if (Part.InstrumentId == 27) return 2.To4();
        if (Part.InstrumentId == 1024) return 9.To4();

        if (InstrumentChannels.TryGetValue(Part.InstrumentId, out int assignedChannel))
        {
            return (FourBitNumber)assignedChannel;
        }

        if (Part.InstrumentId == 0 || Part.InstrumentId == 48 || Part.InstrumentId == 34) // piano and sampler
        {
            return (FourBitNumber)(int)StringNumber;
        }
        var id = Part.InstrumentId;

        if (id >= 0 && id <= 7) return 0.To4(); // Piano -> Ch 1
        if (id >= 24 && id <= 34) return 1.To4(); // Guitar -> Ch 2
        if (id >= 32 && id <= 39) return 2.To4(); // Bass   -> Ch 3
        if (id >= 40 && id <= 55) return 3.To4(); // Strings/Voices -> Ch 4
        if (id >= 56 && id <= 71) return 4.To4(); // Brass/Reeds -> Ch 5
        if (id >= 16 && id <= 23) return 5.To4(); // Organ  -> Ch 6

        return 6.To4();
    }

    private static readonly IReadOnlyDictionary<int, int> InstrumentChannels = new Dictionary<int, int>
    {
        [71] = 1, // Clarinet (used for vocals) -> Ch 5
        [68] = 4, // Oboe (used for vocals) -> Ch 5
        [52] = 4, // Choir Aahs -> Ch 5
        [53] = 4, // Voice Oohs -> Ch 5
        [54] = 4, // Synth Voice -> Ch 5
        [1024] = 9, // Standard Drums
        [127] = 9, // Gunshot (sometimes used as a snare marker)
        [119] = 8, // Reverse Cymbal -> Ch 9
        [122] = 8, // Seashore -> Ch 9
    };
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
    public Nóta Source { get; }
    public Nóta Destination { get; }
    public IReadOnlyList<Nóta> InBetweenNotes { get; }
    public IReadOnlyList<Nóta> FullChain { get; }
    public Time FullDuration { get; }

    public TieContext(Nóta destinationNote)
    {
        if (!destinationNote.Tie || destinationNote.WillBeTied) throw new Exception("no");

        FullChain = destinationNote.GetTies().Reverse().ToList();
        Source = FullChain[0];
        Destination = FullChain[^1];
        InBetweenNotes = FullChain.Skip(1).Take(FullChain.Count - 2).ToList();
        FullDuration = new Time(FullChain.Sum(e => e.ActualDuration.Tick));
    }
}