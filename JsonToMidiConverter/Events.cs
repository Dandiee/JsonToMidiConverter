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
    public static bool SuspendValidation;
    public List<TimedEvent> TimedEvents { get; private set; } = new();
    public IReadOnlyList<TimedEvent> Recap { get; private set; }
    public TimedEvent? LastEvent { get; private set; }
    public Nóta? LastNote { get; private set; }
    public List<(TimedEvent TimedEvent, Nóta Note, long EndTick)> NoteOns { get; private set; } = new();

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
            var w = note?.GetNoteChannel();
            channelEvent.Channel = (channelOverride ?? note.Channel).To4();
        }

        Recap = TimedEvents.Skip(Math.Max(0, TimedEvents.Count - 30)).ToList();

        var eventType = midiEvent.GetType();

        if (!SuspendValidation)
        {
            var pid = partId ?? note.Part.Index;

            //if (pid < 10)
            {
                var refEvent = MidiConverter.ReferenceData[pid][TimedEvents.Count];

                var TIIIME = refEvent.AbsoluteTime;
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
        if (note != null && !SuspendValidation)
        {
            if (newEvent.Event is NoteOnEvent on)
            {
                //if (newEvent.Time == 1474683)
                if (newEvent.Time == 2919360 && note.Part.Index == 8)
                {
                    //var stepSize = note.GetShiftStepSizeTicks();
                }

                if (note.vibrato)
                {

                }

                var ms = note.Beat.AbsoluteBeatStartTime;

                var noteDuration = note.ActualDuration.Tick;
                if (note.WillBeTied) noteDuration = note.GetForwardTies().Sum(e => e.ActualDuration.Tick);
                if (note.Slide != Slide.None && note.Slide != Slide.Legato) noteDuration = note.GetShiftStepSizeTicks();
                if (note.Slide == Slide.Legato) noteDuration = note.vibrato ? noteDuration / 2 : noteDuration;
                //if (note.vibrato) noteDuration = 960;

                var endTime = newEvent.Time + noteDuration;

                NoteOns.Add(new (newEvent, note, endTime));
            }
            else if (newEvent.Event is NoteOffEvent off)
            {
                var pair = NoteOns.Single(e => ((NoteOnEvent)e.TimedEvent.Event).NoteNumber == off.NoteNumber);
                NoteOns.Remove(pair);
            }

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

    
}