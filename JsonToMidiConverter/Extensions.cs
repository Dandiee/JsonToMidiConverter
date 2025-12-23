using Dani.Data;
using Dani.Data.Models;
using Dani.Data.Models.Enums;
using Dani.Data.Models.Parts;
using JsonToMidiConverter.Models;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Part = Dani.Data.Models.Parts.Part;

namespace JsonToMidiConverter;

[DebuggerDisplay("{EventIndex}")]
public sealed class TimedNoteEvent
{
    public int MeasureIndex { get; }
    public int EventIndex { get; }

    public long Start { get; }
    public long End { get; }
    public long Duration { get; }

    public NoteOnEvent On { get; }
    public NoteOffEvent Off { get; }

    public List<PitchBending> PitchBends { get; } = [];

    public bool IsFuckedUp { get; }

    public TimedNoteEvent(int measureIndex, int eventIndex, TimedEvent on, TimedEvent off, bool isSongsterSpecialPieceOfShit, List<TimedEvent> pitchBends)
    {
        MeasureIndex = measureIndex;
        EventIndex = eventIndex;
        Start = on.Time;
        End = off.Time;
        Duration = End - Start;
        On = (NoteOnEvent)on.Event;
        Off = (NoteOffEvent)off.Event;
        IsFuckedUp = isSongsterSpecialPieceOfShit;
        PitchBends = pitchBends.Select(e => new PitchBending(e.Time, ((PitchBendEvent)e.Event).PitchValue)).ToList();
    }


    public bool IsMatching(int channel, int noteNumber) => On.Channel == channel && On.NoteNumber == noteNumber;
}

public record PitchBending(long Time, ushort Value);

public static class Extensions
{
    public static readonly float[] HarmonicFretPalette =
    [
        0f, 12f, // Most common defaults
        
        // Integers
        1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f,
        11f, 13f, 14f, 15f, 16f, 17f, 18f, 19f, 20f,
        21f, 22f, 23f, 24f, 26f, 28f, 29f, 35f, 40f, -1f,

        // Decimals (The Physics Nodes)
        2.4f, 2.7f,
        3.2f,
        4.4f, 4.7f,
        5.2f, 5.7f, 5.8f,
        6.2f,
        8.2f, 8.4f,
        9.6f,
        11.8f,
        14.7f,
        19.6f,
        21.7f
    ];

    public static SevenBitNumber To7(this int i) => (SevenBitNumber)i;
    public static FourBitNumber To4(this int i) => (FourBitNumber)i;

    public static Time Sum(this IEnumerable<Time> times) => new(times.Sum(e => e.Tick));

    public static TimedEvent ToTimed(this MidiEvent midiEvent, Time time) => new(midiEvent, time.Tick);

    public static IReadOnlyList<TimedNoteEvent> GetEvents(this MidiFile midi, int partIndex)
    {
        var chunk = midi.Chunks.OfType<TrackChunk>().ToList()[partIndex];
        var timedEvents = new List<TimedNoteEvent>();

        var time = 0L;
        var measureIndex = 0;

        var noteOns = new List<(int Index, TimedEvent On, bool IsFuckedUp)>();

        var pitchBends = new Dictionary<int, List<TimedEvent>>();

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
                noteOns.Add(new(i, new TimedEvent(noteOn, time), fuckedUpNotes.Count > 0));
            }
            else if (midiEvent is NoteOffEvent noteOff)
            {
                var pair = noteOns.First(e =>
                {
                    var on = (NoteOnEvent)e.On.Event;
                    return on.Channel == noteOff.Channel && on.NoteNumber == noteOff.NoteNumber;
                });

                noteOns.Remove(pair);

                pitchBends.TryGetValue(noteOff.Channel, out var bends);

                timedEvents.Add(new TimedNoteEvent(measureIndex, pair.Index, pair.On, new TimedEvent(noteOff, time), pair.IsFuckedUp, bends ?? []));
                pitchBends[noteOff.Channel] = [];
            }
            else if (midiEvent is PitchBendEvent pitch && pitch.PitchValue != 8192)
            {
                if (!pitchBends.TryGetValue(pitch.Channel, out var bends))
                {
                    bends = [];
                    pitchBends[pitch.Channel] = bends;
                }

                bends.Add(new TimedEvent(pitch, time));
            }
        }

        if (noteOns.Count > 0) throw new Exception();

        return timedEvents
            .OrderBy(e => e.MeasureIndex)
            .ThenBy(e => e.EventIndex)
            .ToList();
    }

    private static readonly int[] _velocityLadder = { 45, 55, 67, 80, 87, 95, 105, 112 };
    private static readonly Dictionary<Velocity, int> _dynamicMap = new Dictionary<Velocity, int>
        {
            { Velocity.Ppp, 0 }, { Velocity.Pp, 1 }, { Velocity.P, 2 }, { Velocity.Mp, 3 },
            { Velocity.Mf, 4 },  { Velocity.F, 5 },  { Velocity.Ff, 6 }, { Velocity.Fff, 7 }
        };

    //private static string _currentDynamic = "f"; // Default to 'f' if no start value provided

    public static int CalculateVelocity2(this Nota input, bool isPrimary)
    {
        // 1. Update State: If the beat has a new original velocity, update our current dynamic
        var currentDynamic = input.Beat.CalculatedVelocity;

        // 2. Get Base Index
        int index = _dynamicMap.GetValueOrDefault(currentDynamic, 5);

        // 3. Apply Modifiers
        // Note: Check if Note_Acentuated is an integer or boolean in your raw input. 
        // The CSV implies 0 (None), 1 (Normal), 2 (Heavy/Marcato).
        // RULE UPDATE: Ghost notes override Accents. 
        if (input.Ghost)
        {
            if (input.Beat.Voice.Measure.Part.IsDrum)
            {
                if (input.Beat.Notes.Count > 1)
                {
                    return 55;
                }
                else
                {
                    index -= 4;
                }
            }
            else
            {
                index -= 2;
            }
            // Do NOT apply accent modifiers here.
        }
        else
        {
            // Only apply accent if it's NOT a ghost note
            if (input.Accentuated == Accent.None) index += 1;
            if (input.Accentuated == Accent.Heavy) index += 2;
        }

        if (input.IsHpTarget) index -= 1;
        if (input.Beat.Technique == Technique.Tap) index -= 1;
        if (input.Harmonic == Harmonic.Tapped) index--;

        if (!isPrimary) index -= 1;

        // 4. Clamp Index to valid range [0, 7]
        if (index < 0) index = 0;
        if (index > 7) index = 7;

        // 5. Lookup Result
        return _velocityLadder[index];
    }

    public static int GetHarmonicOffset(float fret)
    {
        // 1. Convert Fret to Physical String Position (Ratio from Nut)
        // Formula: position = 1 - (1 / 2^(fret/12))
        var position = 1.0f - (float)Math.Pow(2.0f, -fret / 12.0f);

        // 2. Find the matching Harmonic Number (N)
        // We scan harmonics 2 (octave) through 8 (3 octaves) to find the closest fit.
        // We look for a node k/N that matches the string position.

        int bestHarmonic = 0;
        var minDifference = float.MaxValue;

        // Iterate through harmonics 2 to 8 (Standard guitar harmonics range)
        for (int n = 2; n <= 8; n++)
        {
            // For each harmonic N, there are N-1 nodes (k) along the string
            for (int k = 1; k < n; k++)
            {
                var targetNode = (float)k / n;
                var diff = Math.Abs(position - targetNode);

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
        var exactOffset = 12.0f * (float)Math.Log(bestHarmonic, 2);

        // Round to nearest integer to match the Dictionary (e.g. 27.86 -> 28)
        return (int)Math.Round(exactOffset);
    }

    public static int GetNoteNumber(this Nota note, bool withHarmonic = true)
    {
        var part = note.Beat.Voice.Measure.Part;

        if (note.Rest) return 0;

        if (part.IsDrum || (int)note.StringNumber == -1)
        {
            return DrumMapping.Mapping.TryGetValue(note.Fret, out var noteNumber) ? noteNumber.NoteNumber : note.Fret; // default to Acoustic Bass Drum
        }

        var open = part.Tuning.Count == 0 ? (int)note.StringNumber : part.Tuning[(int)note.StringNumber];
        if (note.Harmonic == Harmonic.None || !withHarmonic) return open + note.Fret;
        var harmonicOffset = GetHarmonicOffset(note.HarmonicFret);
        if (note.Harmonic == Harmonic.Natural) return open + harmonicOffset;
        return Math.Min(127, open + harmonicOffset + note.Fret);
    }

    public static FourBitNumber GetNoteChannel(this Nota note) => note.Beat.Voice.Measure.Part.IsDrum ? 9.To4() : (FourBitNumber)note.StringNumber;

    public static Time ToTime(this MusicalFraction fraction) => new(fraction.Nominator, fraction.Denominator);

    public static readonly HashSet<ushort> PianoLikeInstruments = new() { 0, 48, 1024, 67, 66 };

    public static bool IsPianoLike(this Part part) => PianoLikeInstruments.Contains(part.InstrumentId);

    public static bool Has(this SlideFlags flags, SlideFlags value) => (flags & value) != 0;

    public static IEnumerable<SlideFlags> GetUniques(this SlideFlags flags)
    {
        for (int i = 0; i < 8; i++)
        {
            int singleBitMask = 1 << i;
            if (((int)flags & singleBitMask) != 0)
            {
                yield return (SlideFlags)singleBitMask;
            }
        }
    }

    public static readonly HashSet<char> WeirdoCharacters = new[] { '/', '?', '_' }.ToHashSet();

    public static string Clean(this string str)
    {
        var result = str;
        foreach (var weirdoCharacter in WeirdoCharacters)
        {
            result = result.Replace(weirdoCharacter.ToString(), "");
        }

        return result;
    }

    public static string FromClean(this string str)
    {
        var result = str;
        foreach (var weirdoCharacter in WeirdoCharacters)
        {
            result = result.Replace(weirdoCharacter.ToString(), "");
        }

        return result;
    }

    public static string GetPath(this Record record, string root, string fileName)
        => Path.Combine(root, record.Artist.Clean(), record.Title.Clean(), fileName);
}