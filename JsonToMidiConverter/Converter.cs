using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using JsonToMidiConverter.Models;
using JsonToMidiConverter.Test;

namespace JsonToMidiConverter;

internal static class Converter
{
    public const int TicksPerQuarter = 15360;
    public static readonly TicksPerQuarterNoteTimeDivision Tpqn = new(TicksPerQuarter);
    public const int TicksPer64Th = 960; // The "Magic Grid" unit
    public const int MsPer64Th = TicksPerQuarter / TicksPer64Th;


    // Standard MIDI Values
    private const ushort PitchBendCenter = 8192;

    public static readonly SevenBitNumber DefaultVelocity = 112.To7();

    public static MidiFile Convert(Song song, RecordModel record)
    {
        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarter) };
        Time.Map = song.Parts[0].GetTempo(midiFile);
        midiFile.ReplaceTempoMap(Time.Map);

        foreach (var part in song.Parts)
        {
            var events = new Events();
            AddTrackHeader(events, part);

            foreach (var measure in part.Measures)
            {
                AddMeasureMarker(events, measure);
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

                    events.Add(new PitchBendEvent(PitchBendCenter) { Channel = note.Channel.To4() }, cursor);
                    events.Add(new NoteOnEvent(pitch.To7(), velocity.To7()) { Channel = note.Channel.To4() }, cursor);
                    events.Add(new NoteOffEvent(pitch.To7(), velocity.To7()) { Channel = note.Channel.To4() }, cursor + step);

                    cursor += step;
                }

                if (note.Bend != null || note.Beat.TremoloBar != null)
                {
                    var bends = note.GenerateBends((int)note.Duration.Tick);
                    foreach (var bend in bends)
                    {
                        events.Add(new PitchBendEvent((ushort)bend.Value), note.Start + bend.Tick);
                    }
                }

                if (note.Slides.Count > 0 && emittedPitches.Count == 1)
                {
                    var target = Nota.GetSlideTargetPitch(note.Slides[0], note);
                    var bends = PitchBendGenerator.GenerateSlide((int)note.Duration.Tick, note.Fret, target);
                    foreach (var bend in bends)
                    {
                        events.Add(new PitchBendEvent((ushort)bend.Value), note.Start + bend.Tick);
                    }
                }
            }

            var timedEvents = events.TimedEvents
                .OrderBy(e => e.Event.Time)
                .ToList();

            var lastEventTime = new Time();
            foreach(var timedEvent in timedEvents)
            {
                var relativeTime = timedEvent.Event.Time - lastEventTime.Tick;
                timedEvent.Event.Event.DeltaTime = relativeTime;
                lastEventTime = new Time(timedEvent.Event.Time);
            }


            midiFile.Chunks.Add(new TrackChunk(timedEvents.Select(e => e.Event.Event)));
        }

        var dumpFileName = Dumper.GetFileName("Output", record).Replace(".js", ".mid");
        midiFile.Write(dumpFileName, true);

        return midiFile;
    }

    public static void AddMeasureMarker(Events events, Measure measure)
    {
        events.Add(new MarkerEvent($"MEASURE_{measure.Index}"), measure.Start);

        if ((measure.Previous?.Bpm ?? 0) != measure.Bpm)
        {
            events.Add(new SetTempoEvent(measure.Bpm), measure.Start);
        }

        if (measure.Index > 0 && measure.SignatureArray.Count > 0)
        {
            events.Add(new TimeSignatureEvent((byte)measure.Signature.Span.Numerator, (byte)measure.Signature.Span.Denominator), measure.Start);
        }
    }

    public static void AddTrackHeader(Events events, Part part)
    {
        var timeZero = new Time();

        var channels = part.InstrumentId == 1024
            ? [9]
            : Enumerable.Range(0, 9).ToArray();

        foreach (var i in channels)
        {
            // Program Change
            events.Add(new ProgramChangeEvent(part.InstrumentId.To7()) { Channel = i.To4() }, timeZero);
        }

        foreach (var i in channels)
        {
            // Mod Wheel Reset
            events.Add(new ControlChangeEvent(1.To7(), 0.To7()) { Channel = i.To4() }, timeZero);
        }

        foreach (var i in channels)
        {
            // Pitch Bend Reset
            events.Add(new PitchBendEvent(8192) { Channel = i.To4() }, timeZero);
        }

        foreach (var i in channels)
        {
            // RPN Pitch Range Setup (Your 4 events)
            events.Add(new ControlChangeEvent(101.To7(), 0.To7()) { Channel = i.To4() }, timeZero);
            events.Add(new ControlChangeEvent(100.To7(), 0.To7()) { Channel = i.To4() }, timeZero);
            events.Add(new ControlChangeEvent(6.To7(), 24.To7()) { Channel = i.To4() }, timeZero);
            events.Add(new ControlChangeEvent(38.To7(), 0.To7()) { Channel = i.To4() }, timeZero);
        }


        if (!string.IsNullOrEmpty(part.Name))
        {
            events.Add(new SequenceTrackNameEvent(part.Name), timeZero);
        }

        if (!string.IsNullOrEmpty(part.Instrument))
        {
            events.Add(new InstrumentNameEvent(part.Instrument), timeZero);
        }
    }
}