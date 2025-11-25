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
        TimedEvents.Add(new TimedEvent(midiEvent, time.Tick));
        //if (note != null && !SuspendValidation)
        //{
        //    MarkForDeath(newEvent, note);
        //
        //    note.Events.Add(newEvent);
        //}

        //if (noteNumberOverride != null)
        //{
        //    note.NoteNumber = origNoteNumber.Value;
        //}

        LastNote = note;

        return null;
    }

    private void MarkForDeath(TimedEvent timedEvent, Nóta note)
    {
        if (timedEvent.Event is NoteOnEvent on)
        {
            if (note.Is("N0 B5 M47 P8"))
            {

            }

            var noteDuration = note.ActualDuration.Tick;

            if (note.WillBeTied)
            {
                noteDuration = note.GetForwardTies().Sum(e => e.ActualDuration.Tick);
            }

            if (note.Slide != Slide.None)
            {
                
                var slide = note.Beat.Notes[0].GetSlide();
                noteDuration = slide.IsStepped 
                    ? slide.StepDuration.Tick 
                    : slide.HoldDuration.Tick;
            }

            var endTime = timedEvent.Time + noteDuration;

            NoteOns.Add(new(timedEvent, note, endTime));
        }
        else if (timedEvent.Event is NoteOffEvent off)
        {
            var pair = NoteOns.Single(e => ((NoteOnEvent)e.TimedEvent.Event).NoteNumber == off.NoteNumber);
            NoteOns.Remove(pair);
        }
    }


}