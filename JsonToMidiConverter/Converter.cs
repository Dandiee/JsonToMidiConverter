using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Diagnostics;
using System.Reflection;
using JsonToMidiConverter.Test;
using Slide = JsonToMidiConverter.Context.Slide;

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

    public static MidiFile Convert(Song song, MidiFile referenceMidi)
    {
        //Dumper.AssignNotesToMidiEvents(song, referenceMidi);

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

                foreach (var voice in measure.Voices)
                {
                    foreach (var beat in voice.Beats)
                    {
                        foreach (var note in beat.Notes.Where(e => !e.Rest))
                        {


                        }

                    }
                }
            }
            //midiFile.Chunks.Add(events.ToTrackChunk());
        }

        return midiFile;
    }

    public static void On(
        Events events,
        Nota note,
        int noteNumber,
        Time from,
        Time to)
    {
        var part = note.Part;
        events.Add(new PitchBendEvent(PitchBendCenter), from, note);
        events.Add(new NoteOnEvent((SevenBitNumber)noteNumber, DefaultVelocity), from, note);
        events.Add(new NoteOffEvent((SevenBitNumber)noteNumber, DefaultVelocity), to, note);

        var sourceNote = note.Tie
            ? note.TieDetails.Source
            : note;

       
        var matchingEvent = sourceNote.MidiNoteEvents.Single(e => e.IsMatching(note.Channel, noteNumber));

        var acceptableDrift = (note.Index + 1) * 20;
        var onDistance = Math.Abs(matchingEvent.Start - from.Tick);
        var offDistance = Math.Abs(matchingEvent.End - to.Tick);
        //Debug.Assert(onDistance < acceptableDrift);
        //Debug.Assert(offDistance < acceptableDrift);
    }

    public static void AddMeasureMarker(Events events, Measure measure)
    {
        events.Add(new MarkerEvent($"MEASURE_{measure.Index}"), measure.Start, null, null, measure.Part.PartId);

        var measureChange = measure.Part.Automations.Tempo.SingleOrDefault(e => e.Measure == measure.Index);
        if (measureChange != null && measure.Index != 0)
        {
            var newTempo = Tempo.FromBeatsPerMinute(measureChange.Bpm).MicrosecondsPerQuarterNote;
            events.Add(new SetTempoEvent(newTempo), measure.Start, null, null, measure.Part.PartId);
        }

        if (measure.Index > 0 && measure.SignatureArray.Count > 0)
        {
            events.Add(new TimeSignatureEvent((byte)measure.Signature.Span.Numerator, (byte)measure.Signature.Span.Denominator), measure.Start, null, null, measure.Part.PartId);
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
            events.Add(new ProgramChangeEvent(part.InstrumentId.To7()), timeZero, null, i, part.PartId);
        }

        foreach (var i in channels)
        {
            // Mod Wheel Reset
            events.Add(new ControlChangeEvent(1.To7(), 0.To7()), timeZero, null, i, part.PartId);
        }

        foreach (var i in channels)
        {
            // Pitch Bend Reset
            events.Add(new PitchBendEvent(8192), timeZero, null, i, part.PartId);
        }

        foreach (var i in channels)
        {
            // RPN Pitch Range Setup (Your 4 events)
            events.Add(new ControlChangeEvent(101.To7(), 0.To7()), timeZero, null, i, part.PartId);
            events.Add(new ControlChangeEvent(100.To7(), 0.To7()), timeZero, null, i, part.PartId);
            events.Add(new ControlChangeEvent(6.To7(), 24.To7()), timeZero, null, i, part.PartId);
            events.Add(new ControlChangeEvent(38.To7(), 0.To7()), timeZero, null, i, part.PartId);
        }


        if (!string.IsNullOrEmpty(part.Name))
        {
            events.Add(new SequenceTrackNameEvent(part.Name), timeZero, null, null, part.PartId);
        }

        if (!string.IsNullOrEmpty(part.Instrument))
        {
            events.Add(new InstrumentNameEvent(part.Instrument), timeZero,
                null, null, part.PartId);
        }
    }
}