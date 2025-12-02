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
                    var on = e.On.Event as NoteOnEvent;
                    return on.Channel == noteOff.Channel && on.NoteNumber == noteOff.NoteNumber;
                });

                noteOns.Remove(pair);

                timedEvents.Add(new TimedNoteEvent(measureIndex, i, pair.On, new TimedEvent(noteOff, time)));
            }
        }

        return timedEvents;
    }

    public static Slide ToSlide(this string str) => str switch
    {
        "upwards" => Slide.Upwards,
        "downwards" => Slide.Downwards,
        "shift" => Slide.Shift,
        "legato" => Slide.Legato,
        "below" => Slide.Below,
        "above" => Slide.Above,
        "belowlegato" => Slide.BelowLegato,
        "belowdownwards" => Slide.BelowDownwards,
        "belowshift" => Slide.BelowShift,
        "" => Slide.None,



        _ => throw new Exception(),
    };

    private static readonly HashSet<Slide> BackwardSlides = new[]
    {
        Slide.BelowShift, Slide.Below, Slide.BelowDownwards, Slide.BelowLegato, Slide.Above
    }.ToHashSet();

    private static readonly HashSet<Slide> ForwardSlides = new[]
    {
        Slide.Downwards, Slide.BelowDownwards, Slide.Upwards
    }.ToHashSet();

    public static bool IsBackwardSlide(this Slide slide) => BackwardSlides.Contains(slide);
    public static bool IsForwardSlide(this Slide slide) => ForwardSlides.Contains(slide);

    public static bool Is<TMidiEvent>(this TimedEvent timedEvent)
        where TMidiEvent : MidiEvent
        => timedEvent.Event is TMidiEvent;

    public static TMidiEvent As<TMidiEvent>(this TimedEvent timedEvent)
        where TMidiEvent : MidiEvent
    {
        if (timedEvent.Event is TMidiEvent typedEvt)
        {
            return typedEvt;
        }

        throw new InvalidCastException($"Cannot cast event of type {timedEvent.GetType().Name} to {typeof(TMidiEvent).Name}");
    }

    public static TMidiEvent As<TMidiEvent>(this MidiEvent midiEvent)
        where TMidiEvent : MidiEvent
        => (TMidiEvent)midiEvent;

    public static bool Is<TMidiEvent>(this MidiEvent timedEvent)
        where TMidiEvent : MidiEvent
        => timedEvent is TMidiEvent;
}