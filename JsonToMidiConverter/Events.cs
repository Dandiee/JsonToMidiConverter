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

public record ProcessedEvent(TimedEvent Event, Nóta Note);

[DebuggerDisplay("[{TimedEvents.Count} Last at [{LastEvent.Time}]: {LastNote}]")]
public class Events : IEnumerable<ProcessedEvent>
{
    public static bool SuspendValidation;
    public List<ProcessedEvent> TimedEvents { get; private set; } = new();
    public IReadOnlyList<ProcessedEvent> Recap { get; private set; }
    public ProcessedEvent? LastEvent { get; private set; }
    public Nóta? LastNote { get; private set; }

    public ProcessedEvent this[int index] => TimedEvents[index];
    public int Count => TimedEvents.Count;
    public IEnumerator<ProcessedEvent> GetEnumerator() => TimedEvents.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public TimedEvent Add(
        MidiEvent midiEvent,
        Time time,
        Nóta? note,
        int? channelOverride = null,
        int? partId = null,
        SevenBitNumber? noteNumberOverride = null)
    {
        if (noteNumberOverride != null)
        {
            note.NoteNumber = noteNumberOverride.Value;
        }

        if (midiEvent is ChannelEvent channelEvent)
        {
            var w = note?.GetNoteChannel();
            channelEvent.Channel = (channelOverride ?? note.Channel).To4();
        }
        Recap = TimedEvents.Skip(Math.Max(0, TimedEvents.Count - 30)).ToList();
        TimedEvents.Add(new ProcessedEvent(new TimedEvent(midiEvent, time.Tick), note));
        LastNote = note;

        return null;
    }



}