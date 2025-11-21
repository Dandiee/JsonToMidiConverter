using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace JsonToMidiConverter;

[DebuggerDisplay("[{TimedEvents.Count} Last at [{LastEvent.Time}]: {LastNote}]")]
public class Events : IEnumerable<TimedEvent>
{
    public static readonly List<(long AbsoluteTime, MidiEvent Event)>[] ReferenceData = GetReferenceMidiData();
    public static bool SuspenseValidation;
    public List<TimedEvent> TimedEvents { get; private set; } = new();
    public IReadOnlyList<TimedEvent> Recap { get; private set; }
    public TimedEvent? LastEvent { get; private set; }
    public Nóta? LastNote { get; private set; }

    public TimedEvent this[int index] => TimedEvents[index];
    public int Count => TimedEvents.Count;
    public IEnumerator<TimedEvent> GetEnumerator() => TimedEvents.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public TimedEvent Add(
        MidiEvent midiEvent, 
        Time time,
        Nóta? note, 
        int? channelOverride = null, 
        int? partId = null, 
        SevenBitNumber? noteNumberOverride = null)
    {

        var origNoteNumber = note?.NoteNumber;
        if (noteNumberOverride != null)
        {
            note.NoteNumber = noteNumberOverride.Value;
        }

        if (midiEvent is ChannelEvent channelEvent)
        {
            channelEvent.Channel = (channelOverride ?? note.Channel).To4();
        }

        Recap = TimedEvents.Skip(Math.Max(0, TimedEvents.Count - 30)).ToList();

        var eventType = midiEvent.GetType();

        if (!SuspenseValidation)
        {
            var pid = partId ?? note.Part.Index;

            if (pid < 10)
            {
                var refEvent = ReferenceData[pid][TimedEvents.Count];

                var refType = refEvent.Event.EventType;
                var ourType = midiEvent.EventType;
                Debug.Assert(refType == ourType);

                if (!(midiEvent is PitchBendEvent pitch && pitch.PitchValue == 8888))
                {
                    var diff = refEvent.AbsoluteTime - time.Tick;
                    if (Math.Abs(diff) > 9)
                    {
                        Debug.Assert(refEvent.AbsoluteTime == time.Tick);
                    }
                }

                if (refType == ourType)
                {
                    var props = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props)
                    {
                        var propName = prop.Name;
                        var referenceValue = prop.GetValue(refEvent.Event)!;
                        var actualValue = prop.GetValue(midiEvent)!;

                        if (propName != "DeltaTime" && propName != "Velocity" && propName != "PitchValue")
                        {
                            if (!(propName == "PitchValue" && actualValue.ToString() == "8888"))
                            {
                                Debug.Assert(referenceValue.ToString() == actualValue.ToString(), propName);
                            }
                        }
                    }
                }
            }
        }

        var newEvent = new TimedEvent(midiEvent, time.Tick);
        TimedEvents.Add(newEvent);
        if (note != null)
        {
            note.Events.Add(newEvent);
        }

        if (noteNumberOverride != null)
        {
            note.NoteNumber = origNoteNumber.Value;
        }

        LastNote = note;
        LastEvent = newEvent;

        return newEvent;
    }

    private static List<(long AbsoluteTime, MidiEvent Event)>[] GetReferenceMidiData()
    {
        var referenceMidi = MidiFile.Read("ReferenceOutput.mid");
        var results = new List<(long AbsoluteTime, MidiEvent Event)>[referenceMidi.Chunks.Count];

        for (var i = 0; i < referenceMidi.Chunks.Count; i++)
        {
            results[i] = new List<(long AbsoluteTime, MidiEvent Event)>();
            var time = 0l;
            foreach (var midiEvent in (referenceMidi.Chunks[i] as TrackChunk)!.Events)
            {

                if (time == 0 && (midiEvent is TimeSignatureEvent || midiEvent is SetTempoEvent))
                {
                    continue;
                }

                time += midiEvent.DeltaTime;
                results[i].Add(new(time, midiEvent));

            }
        }

        return results;
    }

}