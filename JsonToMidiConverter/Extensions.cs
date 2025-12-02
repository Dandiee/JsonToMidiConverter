using System.Diagnostics;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Slide = JsonToMidiConverter.Context.Slide;

namespace JsonToMidiConverter;

public record TimedMidiEvent(int Index, long Time, MidiEvent Event);

public static class Extensions
{
    public static SevenBitNumber To7(this int i) => (SevenBitNumber)i;
    public static FourBitNumber To4(this int i) => (FourBitNumber)i;

    public static IReadOnlyList<TimedMidiEvent> GetEvents(this MidiFile midi, int partIndex)
    {
        var chunk = midi.Chunks.OfType<TrackChunk>().ToList()[partIndex];
        var timedEvents = new List<TimedMidiEvent>();

        var time = 0L;
        for (var i = 0; i < chunk.Events.Count; i++)
        {
            var midiEvent = chunk.Events[i];
            time += midiEvent.DeltaTime;
            timedEvents.Add(new TimedMidiEvent(i, time, midiEvent));
        }

        return timedEvents;
    }

    public static IReadOnlyList<TimedMidiEvent> GetMeasureEvents(this IReadOnlyList<TimedMidiEvent> events, Measure measure) =>
        events
            .SkipWhile(e => !(e.Event is MarkerEvent marker && marker.Text == $"MEASURE_{measure.Index}"))
            .TakeWhile(e => e.Event is not MarkerEvent marker || marker.Text == $"MEASURE_{measure.Index}")

            // Time based implementation
            // .SkipWhile(e => !(e.Event is MarkerEvent marker && Math.Abs(e.Time - measure.StartTime.Tick) < 10))
            // .TakeWhile(e => e.Event is not MarkerEvent marker || Math.Abs(e.Time - measure.StartTime.Tick) > 100)

            .ToList();

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