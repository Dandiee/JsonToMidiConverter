using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Dani.Data.Models.Enums;

namespace JsonToMidiConverter;

internal static class Converter
{
    public const int TicksPerQuarter = 15360;
    public static readonly TicksPerQuarterNoteTimeDivision Tpqn = new(TicksPerQuarter);


    // Standard MIDI Values
    private const ushort PitchBendCenter = 8192;

    public static MidiFile Convert(Song song)
    {
        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarter) };
        Time.Map = song.Parts[0].TempoMap;
        midiFile.ReplaceTempoMap(Time.Map);

        var usedChannels = new HashSet<int>();

        foreach (var part in song.Parts)
        {
            foreach (var measure in part.Measures)
            {
                AddMeasureMarker(part.TimedEvents, measure);
            }

            var notes = part.Measures
                .SelectMany(e => e.Voices)
                .SelectMany(e => e.Beats)
                .SelectMany(n => n.Notes)
                .Where(e => !e.Rest)
                .OrderBy(e => e.Start.Tick)
                .ToList();

            foreach (var note in notes)
            {
                var emittedPitches = note.GetEmittedNotes().ToList();
                if (emittedPitches.Count == 0)
                {
                    continue;
                }

                var step = note.Duration / emittedPitches.Count;
                var cursor = note.Start;

                foreach (var pitch in emittedPitches)
                {
                    var velocity = note.CalculateVelocity2(pitch == note.NoteNumber);

                    usedChannels.Add(note.Channel);


                    part.TimedEvents.Add(new PitchBendEvent(PitchBendCenter) { Channel = note.Channel.To4() }.ToTimed(cursor));
                    part.TimedEvents.Add(new NoteOnEvent(pitch.To7(), velocity.To7()) { Channel = note.Channel.To4() }.ToTimed(cursor));
                    part.TimedEvents.Add(new NoteOffEvent(pitch.To7(), velocity.To7()) { Channel = note.Channel.To4() }.ToTimed(cursor + step));

                    cursor += step;
                }

                if (note.Bend != null || note.Beat.Tremolo != null)
                {
                    var bends = note.GenerateBends((int)note.Duration.Tick);
                    foreach (var bend in bends)
                    {
                        part.TimedEvents.Add(new PitchBendEvent((ushort)bend.Value) { Channel = note.Channel.To4() }.ToTimed(note.Start + bend.Tick));
                    }
                }

                if (note.Slides != SlideFlags.None  && emittedPitches.Count == 1)
                {
                    // TODO: not sure
                    // var target = Nota.GetSlideTargetPitch(note.Slides[0], note);
                    var target = Nota.GetSlideTargetPitch(note.Slides.GetUniques().First(), note);
                    var bends = PitchBendGenerator.GenerateSlide((int)note.Duration.Tick, note.Fret, target);
                    foreach (var bend in bends)
                    {
                        part.TimedEvents.Add(new PitchBendEvent((ushort)bend.Value) { Channel = note.Channel.To4() }.ToTimed(note.Start + bend.Tick));
                    }
                }
            }
        }



        foreach (var part in song.Parts)
        {
            AddTrackHeader(part, usedChannels);

            var timedEvents = part.TimedEvents
                .OrderBy(e => e.Time)
                .ToList();

            var lastEventTime = new Time();
            foreach (var timedEvent in timedEvents)
            {
                var relativeTime = timedEvent.Time - lastEventTime.Tick;
                timedEvent.Event.DeltaTime = relativeTime;
                lastEventTime = new Time(timedEvent.Time);
            }

            midiFile.Chunks.Add(new TrackChunk(timedEvents.Select(e => e.Event)));

        }


        var dumpFileName = song.Record.GetPath("", "Output.mid");
        var fi = new FileInfo(dumpFileName);
        if (fi.Directory is { Exists: false })
        {
            fi.Directory.Create();
        }

        midiFile.Write(dumpFileName, true);
        return midiFile;
    }

    public static void AddMeasureMarker(List<TimedEvent> events, Measure measure)
    {
        events.Add(new MarkerEvent($"MEASURE_{measure.Index}").ToTimed(measure.Start));

        if ((measure.Previous?.Bpm ?? 0) != measure.Bpm)
        {
            //events.Add(new SetTempoEvent(measure.Bpm).ToTimed(measure.Start));
        }

        if (measure.Index > 0) // TODO: && measure.SignatureArray != null)
        {
            events.Add(new TimeSignatureEvent(measure.SignatureFracture.Nominator, measure.SignatureFracture.Denominator).ToTimed(measure.Start));
        }
    }

    public static void AddTrackHeader(Part part, HashSet<int> usedChannels)
    {
        var timeZero = new Time();

        var channels = part.InstrumentId == 1024
            ? [9]
            : usedChannels;

        foreach (var i in channels)
        {
            // Program Change
            var instru = part.InstrumentId == 1024 
                ? 1024 
                : part.InstrumentId;

            //if (instru > 127) instru = 127; // Default to Acoustic Grand Piano if out of range
            part.TimedEvents.Add(new ProgramChangeEvent(instru.To7()) { Channel = i.To4() }.ToTimed(timeZero));
        }

        foreach (var i in channels)
        {
            // Mod Wheel Reset
            // events.Add(new ControlChangeEvent(1.To7(), 0.To7()) { Channel = i.To4() }, timeZero);
        }

        foreach (var i in channels)
        {
            // Pitch Bend Reset
            //events.Add(new PitchBendEvent(8192) { Channel = i.To4() }, timeZero);
        }

        foreach (var i in channels)
        {
            // RPN Pitch Range Setup (Your 4 events)
            part.TimedEvents.Add(new ControlChangeEvent(101.To7(), 0.To7()) { Channel = i.To4() }.ToTimed(timeZero));
            part.TimedEvents.Add(new ControlChangeEvent(100.To7(), 0.To7()) { Channel = i.To4() }.ToTimed(timeZero));
            part.TimedEvents.Add(new ControlChangeEvent(6.To7(), 24.To7()) { Channel = i.To4() }.ToTimed(timeZero));
            part.TimedEvents.Add(new ControlChangeEvent(38.To7(), 0.To7()) { Channel = i.To4() }.ToTimed(timeZero));
        }


        if (!string.IsNullOrEmpty(part.Name))
        {
            part.TimedEvents.Add(new SequenceTrackNameEvent(part.Name).ToTimed(timeZero));
        }

        if (!string.IsNullOrEmpty(part.Instrument))
        {
            part.TimedEvents.Add(new InstrumentNameEvent(part.Instrument).ToTimed(timeZero));
        }
    }
}