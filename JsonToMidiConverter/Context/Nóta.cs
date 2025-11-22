using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Context;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter.Models.Song;

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
    [JsonIgnore] public Queue<(MidiEvent Event, Time Time)> PendingEvents { get; private set; } = new();

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

        WillBeTied = GetWillBeTied();

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

        return (finalDuration / denominator).Tick;
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



    public void Is(int noteIndex, int beatIndex, int measureIndex, int? partIndex = null)
    {
        if (Index == noteIndex && Beat.Index == beatIndex && Measure.Index == measureIndex &&
            (!partIndex.HasValue || Part.Index == partIndex.Value)) Debugger.Break();
    }

    public int GetNoteNumber()
    {
        if (Part.InstrumentId == 1024 || (int)StringNumber == -1)
        {
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

        // 4. STANDARD FRETTED NOTE
        return (openStringPitch + Fret);
    }

    public SevenBitNumber GetSlideTargetPitch()
    {
        if (Slide == Slide.Shift) return GetSlideTarget().NoteNumber;
        if (Slide == Slide.Downwards)
        {
            var distanceToFret1 = Fret - 1;
            return (SevenBitNumber)(NoteNumber - distanceToFret1);
        }
        if (Slide == Slide.Upwards)
        {
            return (SevenBitNumber)(NoteNumber + 9);
        }
        if (Slide == Slide.Legato) return GetSlideTarget().NoteNumber;

        throw new Exception("what slide");
    }

    public FourBitNumber GetNoteChannel()
    {
        if (Part.InstrumentId == 71 || Part.InstrumentId == 68 
                                    || Part.InstrumentId == 27 
                                    || Part.InstrumentId == 30
                                    || Part.InstrumentId == 40 
                                    || Part.InstrumentId == 29)
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