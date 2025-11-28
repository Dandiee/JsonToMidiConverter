using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Diagnostics;
using System.Reflection;
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
        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarter) };
        Time.Map = song.Parts[0].GetTempo(midiFile);
        midiFile.ReplaceTempoMap(Time.Map);
        song.Build(midiFile);

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

                            if (note.Is("N0 B5 V0 M48 P2"))
                            {

                            }


                            var start = note.GetStartTime();
                            var end = note.GetEndTime();


                            if (note.Vibrato)
                            {
                                if (note.Slide != Slide.None)
                                {
                                    var slide = note.GetSlide();

                                    var note1Start = beat.AbsoluteBeatStartTime;
                                    var note1End = note1Start + slide.HoldDuration;
                                    var note1Pitch = note.NoteNumber;

                                    var note2Start = note1End;
                                    var note2End = note2Start + note.ActualDuration - slide.HoldDuration;
                                    var note2Pitch = note1Pitch + slide.Direction * slide.Steps;

                                    On(events, note, note1Pitch, note1Start, note1End);
                                    On(events, note, note2Pitch, note2Start, note2End);

                                    events.Add(new ControlChangeEvent(1.To7(), 64.To7()), note1Start, note);
                                    events.Add(new ControlChangeEvent(1.To7(), 0.To7()), note1End, note);
                                }
                                else
                                {
                                    On(events, note, note.NoteNumber, start, end);

                                    events.Add(new ControlChangeEvent(1.To7(), 64.To7()), start, note);
                                    events.Add(new ControlChangeEvent(1.To7(), 0.To7()), end, note);
                                }
                            }
                            else if (note.Slide != Slide.None)
                            {
                                var slide = note.GetSlide();

                                if (!slide.IsStepped)
                                {
                                    if (!note.Tie) // tie starts are not ties, attack note is playing already
                                    {
                                        var startNote = note.GetStartTime();
                                        var endNote = note.GetEndTime();
                                        On(events, note, note.NoteNumber, startNote, endNote);
                                    }

                                    end = start + note.ActualDuration;
                                    var step = note.ActualDuration / 2d / 100d;
                                    events.Add(new PitchBendEvent(PitchBendCenter), end - 960, note);
                                    Enumerable.Range(1, 100).ToList().ForEach(i =>
                                    {
                                        events.Add(new PitchBendEvent(PitchBendCenter), end - 960 + (i * step.Tick),
                                            note);
                                    });
                                }
                                else
                                {
                                    var strummul = 1;
                                    if (note.Is("N1 B5 M246 P1"))
                                    {
                                        strummul = 2;
                                    }

                                    var noteStart = note.GetStartTime();
                                    var noteEnd = note.GetEndTime();

                                    var holdFrom = start;
                                    var holdTo = holdFrom + slide.HoldDuration -
                                                 (note.Index * note.GetStrum().Tick / 2) * strummul;

                                    if (slide.PlayHold)
                                    {
                                        On(events, note, note.NoteNumber, holdFrom, holdTo);
                                    }

                                    var startTime = slide.TimeDirection < 0 ? holdFrom : holdTo;

                                    for (var step = 0; step < slide.Steps; step++)
                                    {
                                        var i = slide.TimeDirection < 0 ? step + 1 : step;

                                        var stepFrom = startTime + slide.TimeDirection *
                                            (slide.StepDuration * i - (i * 9 * note.Index));
                                        var stepTo = stepFrom + (slide.StepDuration - 9 * note.Index);
                                        var stepNote = note.NoteNumber + slide.Direction * (step + 1);

                                        On(events, note, stepNote, stepFrom, stepTo);
                                    }
                                }
                            }
                            else if (note.Bend != null)
                            {
                                var noteStart = note.GetStartTime();
                                var noteEnd = note.GetEndTime();
                                var noteDuration = noteEnd - noteStart;
                                var quarterDuration = noteDuration / 4;
                                var pitchBendStep = quarterDuration / 60;

                                var leftoverDuration = noteDuration - quarterDuration;


                                //events.Add(new ControlChangeEvent(1.To7(), 110.To7()), start, note);
                                //events.Add(new ControlChangeEvent(1.To7(), 0.To7()), end, note);

                                if (!note.Tie) // tie roots are not ties, attack note is already playing
                                {
                                    On(events, note, note.NoteNumber, noteStart, noteEnd);
                                }

                                if (note.Is("N0 B0 M127 P1"))
                                {

                                }

                                events.Add(new PitchBendEvent(PitchBendCenter), noteStart + 1, note);
                                Enumerable.Range(1, 61).ToList().ForEach(i =>
                                {
                                    events.Add(new PitchBendEvent(PitchBendCenter), noteStart + pitchBendStep * i,
                                        note);
                                });
                                events.Add(new PitchBendEvent(PitchBendCenter), noteStart + pitchBendStep * 60 + 1,
                                    note);
                            }
                            else if (!note.Tie)
                            {
                                if (note.Is("N0 B2 M212 P1"))
                                {

                                }

                                var noteStart = note.GetStartTime();
                                var noteEnd = note.GetEndTime();
                                Debug.Assert(noteEnd != noteStart);
                                Debug.Assert(noteEnd > noteStart);

                                var w = note.GetNoteNumber();
                                On(events, note, note.NoteNumber, noteStart, note.GetEndTime());
                            }


                            if (note.Bend != null)
                            {

                            }

                        }

                    }
                }
            }

            Validate(events, part, referenceMidi);
            //midiFile.Chunks.Add(events.ToTrackChunk());
        }

        return midiFile;
    }

    public static void On(Events events, Nóta note, int noteNumber, Time from, Time to)
    {
        events.Add(new PitchBendEvent(PitchBendCenter), from, note);
        events.Add(new NoteOnEvent((SevenBitNumber)noteNumber, DefaultVelocity), from, note);
        events.Add(new NoteOffEvent((SevenBitNumber)noteNumber, DefaultVelocity), to, note);
    }

    public static void Validate(Events events, Part part, MidiFile reference)
    {
        var chunk = reference.GetEvents(part.Index);

        foreach (var measure in part.Measures)
        {
            var referenceEvents = chunk.GetMeasureEvents(measure);
            foreach (var referenceEvent in referenceEvents.Where(e => e.Event is NoteEvent))
            {
                var match = events
                    .Where(e => e.Event.Event.EventType == referenceEvent.Event.EventType)
                    .Where(e =>
                        e.Event.Event is NoteEvent on &&
                        referenceEvent.Event is NoteEvent ron &&
                        on.NoteNumber == ron.NoteNumber &&
                        on.Channel == ron.Channel)
                    .OrderBy(e => Math.Abs(e.Event.Time - referenceEvent.Time))
                    .First();

                var acceptableDrift = (match.Note.Index + 1) * 15;
                var distance = Math.Abs(match.Event.Time - referenceEvent.Time);
                var referenceIndex = referenceEvent.Index;
                var partDetails = part.ToString();

                Debug.Assert(acceptableDrift > distance);
            }
        }

    }

    public static void AddMeasureMarker(Events events, Measure measure)
    {
        events.Add(new MarkerEvent($"MEASURE_{measure.Index}"), measure.StartTime, null, null, measure.Part.PartId);

        var measureChange = measure.Part.Automations.Tempo.SingleOrDefault(e => e.Measure == measure.Index);
        if (measureChange != null && measure.Index != 0)
        {
            var newTempo = Tempo.FromBeatsPerMinute(measureChange.Bpm).MicrosecondsPerQuarterNote;
            events.Add(new SetTempoEvent(newTempo), measure.StartTime, null, null, measure.Part.PartId);
        }

        if (measure.Index > 0 && measure.Signature.Count > 0)
        {
            events.Add(new TimeSignatureEvent(measure.SignatureNominator.Value, measure.SignatureDenominator.Value), measure.StartTime, null, null, measure.Part.PartId);
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