using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace JsonToMidiConverter;

[DebuggerDisplay("[{TimedEvents.Count} Last: P{LastNote.Part.Index} M{LastNote.Measure.Index} B{LastNote.Beat.Index}]")]
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

        if (note != null)
        {

        }

        Recap = TimedEvents.Skip(Math.Max(0, TimedEvents.Count - 30)).ToList();

        var eventType = midiEvent.GetType();

        if (!SuspenseValidation)
        {
            var pid = partId ?? note.Part.Index;

            if (pid < 10)
            {
                var referenceChunk = ReferenceData[pid];
                var referenceEvent = referenceChunk[TimedEvents.Count];
                var areTheSameType = referenceEvent.Event.GetType() == eventType;
                Debug.Assert(areTheSameType);

                if (!(midiEvent is PitchBendEvent pitch && pitch.PitchValue == 8888))
                {
                    var diff = referenceEvent.AbsoluteTime - time.Tick;
                    if (Math.Abs(diff) > 8)
                    {
                        Debug.Assert(referenceEvent.AbsoluteTime == time.Tick);
                    }
                }

                if (areTheSameType)
                {
                    var props = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props)
                    {
                        var propName = prop.Name;
                        var referenceValue = prop.GetValue(referenceEvent.Event)!;
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