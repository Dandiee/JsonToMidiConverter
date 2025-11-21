using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using Microsoft.VisualBasic;
using Note = Melanchall.DryWetMidi.Interaction.Note;

namespace JsonToMidiConverter;

internal static partial class MidiConverter
{
    

    public static (TimedEvent Event, Nóta Ctx) Add(this IList<TimedEvent> events, MidiEvent midiEvent, Time time,
        Nóta? note, int? channelOverride = null, int? partId = null, int? noteNumberOverride = null)
    {

        var origNoteNumber = note?.NoteNumber;
        if (noteNumberOverride != null)
        {
            note.NoteNumber = noteNumberOverride.Value;
        }

        if (midiEvent is ChannelEvent channelEvent)
        {
            channelEvent.Channel = (FourBitNumber)(channelOverride ?? note.Channel);
        }

        var lastTen = events.Skip(events.Count - 20).Take(20).ToList();

        var eventType = midiEvent.GetType();

        if (!SuspenseValidation)
        {
            var pid = partId ?? note.Part.Index;

            if (pid < 10)
            {
                var referenceChunk = ReferenceData[pid];
                var referenceEvent = referenceChunk[events.Count];
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
        events.Add(newEvent);
        if (note != null)
        {
            note.Events.Add(newEvent);
        }

        if (noteNumberOverride != null)
        {
            note.NoteNumber = origNoteNumber.Value;
        }

        return (newEvent, note);
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