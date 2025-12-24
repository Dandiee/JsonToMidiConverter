using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Dani.Converter.Dumper;

public static class MidiExtensions
{
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
}