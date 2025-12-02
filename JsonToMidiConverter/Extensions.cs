using JsonToMidiConverter.Models;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Slide = JsonToMidiConverter.Context.Slide;

namespace JsonToMidiConverter;

public sealed class TimedNoteEvent
{
    public int MeasureIndex { get; }
    public int EventIndex { get; }

    public long Start { get;  }
    public long End { get;  }
    public long Duration { get; }

    public NoteOnEvent On { get; }
    public NoteOffEvent Off { get; }

    public TimedNoteEvent(int measureIndex, int eventIndex, TimedEvent on, TimedEvent off)
    {
        MeasureIndex = measureIndex;
        EventIndex = eventIndex;
        Start = on.Time;
        End = off.Time;
        Duration = End - Start;
        On = (NoteOnEvent)on.Event;
        Off = (NoteOffEvent)off.Event;
    }

    public bool IsMatching(int channel, int noteNumber) => On.Channel == channel && On.NoteNumber == noteNumber;
}

public static class Extensions
{
    public static SevenBitNumber To7(this int i) => (SevenBitNumber)i;
    public static FourBitNumber To4(this int i) => (FourBitNumber)i;

    public static IReadOnlyList<TimedNoteEvent> GetEvents(this MidiFile midi, int partIndex)
    {
        var chunk = midi.Chunks.OfType<TrackChunk>().ToList()[partIndex];
        var timedEvents = new List<TimedNoteEvent>();

        var time = 0L;
        var measureIndex = 0;

        var noteOns = new List<(int Index, TimedEvent On)>();

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
                noteOns.Add(new (i, new TimedEvent(noteOn, time)));
            }
            else if (midiEvent is NoteOffEvent noteOff)
            {
                var pair = noteOns.First(e =>
                {
                    var on = (NoteOnEvent)e.On.Event;
                    return on.Channel == noteOff.Channel && on.NoteNumber == noteOff.NoteNumber;
                });

                noteOns.Remove(pair);

                timedEvents.Add(new TimedNoteEvent(measureIndex, i, pair.On, new TimedEvent(noteOff, time)));
            }
        }

        return timedEvents;
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

    public static int GetNoteNumber(this Nota note, bool withHarmonic = true)
    {
        if (note.Rest) return 0;

        if (note.Part.InstrumentId == 1024 || (int)note.StringNumber == -1)
        {
            return DrumMapping.Mapping.TryGetValue(note.Fret, out var noteNumber) ? noteNumber.NoteNumber : note.Fret; // default to Acoustic Bass Drum
        }

        var open = note.Part.Tuning.Length == 0 ? (int)note.StringNumber : note.Part.Tuning[(int)note.StringNumber];
        if (note.Harmonic == null || !withHarmonic) return open + note.Fret;
        var harmonicOffset = FretHarmonicOffsets[note.HarmonicFret];
        if (note.Harmonic.Equals("natural", StringComparison.OrdinalIgnoreCase)) return open + harmonicOffset;
        return open + harmonicOffset + note.Fret;
    }

    public static FourBitNumber GetNoteChannel(this Nota note) => note.Part.InstrumentId == 1024 ? 9.To4() : (FourBitNumber)note.StringNumber;
}