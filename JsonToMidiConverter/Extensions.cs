using System.Diagnostics;
using JsonToMidiConverter.Models;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Slide = JsonToMidiConverter.Context.Slide;

namespace JsonToMidiConverter;

[DebuggerDisplay("{EventIndex}")]
public sealed class TimedNoteEvent
{
    public int MeasureIndex { get; }
    public int EventIndex { get; }

    public long Start { get;  }
    public long End { get;  }
    public long Duration { get; }

    public NoteOnEvent On { get; }
    public NoteOffEvent Off { get; }

    public bool IsFuckedUp { get; }

    public TimedNoteEvent(int measureIndex, int eventIndex, TimedEvent on, TimedEvent off, bool isSongsterSpecialPieceOfShit)
    {
        MeasureIndex = measureIndex;
        EventIndex = eventIndex;
        Start = on.Time;
        End = off.Time;
        Duration = End - Start;
        On = (NoteOnEvent)on.Event;
        Off = (NoteOffEvent)off.Event;
        IsFuckedUp = isSongsterSpecialPieceOfShit;
    }
    

    public bool IsMatching(int channel, int noteNumber) => On.Channel == channel && On.NoteNumber == noteNumber;
}

public static class Extensions
{
    public static SevenBitNumber To7(this int i) => (SevenBitNumber)i;
    public static FourBitNumber To4(this int i) => (FourBitNumber)i;

    public static Time Sum(this IEnumerable<Time> times) => new (times.Sum(e => e.Tick));

    public static IReadOnlyList<TimedNoteEvent> GetEvents(this MidiFile midi, int partIndex)
    {
        var chunk = midi.Chunks.OfType<TrackChunk>().ToList()[partIndex];
        var timedEvents = new List<TimedNoteEvent>();

        var time = 0L;
        var measureIndex = 0;

        var noteOns = new List<(int Index, TimedEvent On, bool IsFuckedUp)>();

        for (var i = 0; i < chunk.Events.Count; i++)
        {
            var midiEvent = chunk.Events[i];
            time += midiEvent.DeltaTime;

            if (midiEvent is MarkerEvent marker && marker.Text.StartsWith("MEASURE_"))
            {
                measureIndex = int.Parse(marker.Text.Split('_')[1]);
            }
            else if (midiEvent is NoteOnEvent noteOn)
            {
                var fuckedUpNotes = noteOns
                    .Where(e =>
                    {
                        var on = (NoteOnEvent)e.On.Event;
                        return on.Channel == noteOn.Channel && on.NoteNumber == noteOn.NoteNumber;
                    })
                    .ToList();

                fuckedUpNotes.ForEach(e => e.IsFuckedUp = true);
                noteOns.Add(new (i, new TimedEvent(noteOn, time), fuckedUpNotes.Count > 0));
            }
            else if (midiEvent is NoteOffEvent noteOff)
            {
                var pair = noteOns.First(e =>
                {
                    var on = (NoteOnEvent)e.On.Event;
                    return on.Channel == noteOff.Channel && on.NoteNumber == noteOff.NoteNumber;
                });

                noteOns.Remove(pair);

                timedEvents.Add(new TimedNoteEvent(measureIndex, pair.Index, pair.On, new TimedEvent(noteOff, time), pair.IsFuckedUp));
            }
        }

        if (noteOns.Count > 0) throw new Exception();

        return timedEvents
            .OrderBy(e => e.MeasureIndex)
            .ThenBy(e => e.EventIndex)
            .ToList();
    }

    public static IEnumerable<Slide> ToSlides(this string slide)
    {
        var rest = slide;

        if (rest.StartsWith("below"))
        {
            yield return Slide.Below;
            rest = rest[5..];
        }
        else if (rest.StartsWith("above"))
        {
            yield return Slide.Above;
            rest = rest[5..];
        }

        if (rest.Length > 0)
        {
            yield return rest switch
            {
                "upwards" => Slide.Upwards,
                "downwards" => Slide.Downwards,
                "shift" => Slide.Shift,
                "legato" => Slide.Legato,

                _ => throw new NotSupportedException()
            };
        }
    }

    public static bool IsBefore(this IEnumerable<Slide> slides)
        => slides.Any(e => e is Slide.Below /*or Slide.Above*/);

    public static readonly IReadOnlyDictionary<double, int> FretHarmonicOffsets = new Dictionary<double, int>
    {
        [2.4] = 36,
        [2.7] = 34,
        [3.2] = 31,
        [3] = 31,
        [4] = 28,
        [5] = 24,
        [5.8] = 34,
        [7] = 19,
        [8.2] = 36,
        [9] = 28,
        [9.6] = 34,
        [12] = 12,
        [16] = 28,
        [19] = 19,
        [21.7] = 34,
        [24] = 24,
    };

    public static int GetHarmonicOffset(double fret)
    {
        // 1. Convert Fret to Physical String Position (Ratio from Nut)
        // Formula: position = 1 - (1 / 2^(fret/12))
        double position = 1.0 - Math.Pow(2.0, -fret / 12.0);

        // 2. Find the matching Harmonic Number (N)
        // We scan harmonics 2 (octave) through 8 (3 octaves) to find the closest fit.
        // We look for a node k/N that matches the string position.

        int bestHarmonic = 0;
        double minDifference = double.MaxValue;

        // Iterate through harmonics 2 to 8 (Standard guitar harmonics range)
        for (int n = 2; n <= 8; n++)
        {
            // For each harmonic N, there are N-1 nodes (k) along the string
            for (int k = 1; k < n; k++)
            {
                double targetNode = (double)k / n;
                double diff = Math.Abs(position - targetNode);

                // If this is the closest node we've found so far, store it.
                // We use a tolerance because frets like '3' are approximations of '3.2'
                if (diff < minDifference)
                {
                    minDifference = diff;
                    bestHarmonic = n;
                }
            }
        }

        // 3. Convert Harmonic Number to Semitone Offset
        // Formula: Offset = 12 * Log2(HarmonicNumber)
        double exactOffset = 12.0 * Math.Log(bestHarmonic, 2);

        // Round to nearest integer to match the Dictionary (e.g. 27.86 -> 28)
        return (int)Math.Round(exactOffset);
    }

    public static int GetNoteNumber(this Nota note, bool withHarmonic = true)
    {
        if (note.Rest) return 0;

        if (note.Part.InstrumentId == 1024 || (int)note.StringNumber == -1)
        {
            return DrumMapping.Mapping.TryGetValue(note.Fret, out var noteNumber) ? noteNumber.NoteNumber : note.Fret; // default to Acoustic Bass Drum
        }

        var open = note.Part.Tuning.Length == 0 ? (int)note.StringNumber : note.Part.Tuning[(int)note.StringNumber];
        if (note.Harmonic == null || !withHarmonic) return open + note.Fret;
        var harmonicOffset = GetHarmonicOffset(note.HarmonicFret);
        if (note.Harmonic.Equals("natural", StringComparison.OrdinalIgnoreCase)) return open + harmonicOffset;
        return open + harmonicOffset + note.Fret;
    }

    public static FourBitNumber GetNoteChannel(this Nota note) => note.Part.InstrumentId == 1024 ? 9.To4() : (FourBitNumber)note.StringNumber;
}