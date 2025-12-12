using JsonToMidiConverter.Context;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Slide = JsonToMidiConverter.Context.Slide;

namespace JsonToMidiConverter;

public record ProcessedEvent(TimedEvent Event);

[DebuggerDisplay("[{TimedEvents.Count} Last at [{LastEvent.Time}]: {LastNote}]")]
public class Events : IEnumerable<ProcessedEvent>
{
    public static bool SuspendValidation;
    public List<ProcessedEvent> TimedEvents { get; private set; } = new();
    public IReadOnlyList<ProcessedEvent> Recap { get; private set; }
    public ProcessedEvent? LastEvent { get; private set; }
    public Nota? LastNote { get; private set; }

    public ProcessedEvent this[int index] => TimedEvents[index];
    public int Count => TimedEvents.Count;
    public IEnumerator<ProcessedEvent> GetEnumerator() => TimedEvents.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public TimedEvent Add(MidiEvent midiEvent, Time time)
    {
        TimedEvents.Add(new ProcessedEvent(new TimedEvent(midiEvent, time.Tick)));
        return null;
    }
}