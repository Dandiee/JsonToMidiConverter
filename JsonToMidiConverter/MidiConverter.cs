using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System.Diagnostics;
using Note = Melanchall.DryWetMidi.Interaction.Note;

namespace JsonToMidiConverter;

internal static partial class MidiConverter
{
    private const int TicksPerQuarterNote = 15360;

    public static readonly SevenBitNumber Velocity = 9.To7();

    public static MidiFile Convert(Song song)
    {
        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote) };
        Time.Map = song.parts[0].GetTempo(midiFile); ;
        song.Build();

        foreach (var part in song.parts)
        {
            var events = new Events();

            BuildHeader(part, events);

            if (part.partId == 10) continue;

            var currentCursor = new Time();

            foreach (var measure in part.measures)
            {
                var measureCursor = new Time(new BarBeatFractionTimeSpan(measure.Index));

                events.Add(new MarkerEvent($"MEASURE_{measure.Index}"), measureCursor, null, null, part.partId);

                var measureChange = part.automations.tempo.SingleOrDefault(e => e.measure == measure.Index);
                if (measureChange != null && measure.Index != 0)
                {
                    events.Add(new SetTempoEvent(Tempo.FromBeatsPerMinute(measureChange.bpm).MicrosecondsPerQuarterNote), measureCursor, null, null, part.partId);
                }

                foreach (var beat in measure.Beats)
                {
                    var beatCursor = measureCursor + beat.GetMeasureStartDuration(Time.Map);

                    currentCursor = beatCursor;

                    foreach (var note in beat.notes.Take(1))
                    {
                        if (note.rest)
                        {
                            currentCursor += beat.MusicalDuration;
                            continue;
                        }

                        currentCursor = AddAttackNote(events, note, currentCursor);

                        currentCursor = AddVibrato(events, note, currentCursor);

                        currentCursor = AddSlide(events, note, currentCursor);
                        

                        currentCursor += 123;
                    }

                    CloseBeat(events, beat);


                }
            }

            var trackChunk = events.ToTrackChunk();
            midiFile.Chunks.Add(trackChunk);
            Debug.WriteLine($"Part {part.Index} finished without error!");
        }
        midiFile.ReplaceTempoMap(Time.Map);
        return midiFile;
    }

    public static Time AddSlide(Events events, Nóta note, Time currentCursor)
    {
        if (note.Slide == Slide.None) return currentCursor;

        var actualDuration = note.ActualDuration;

        // Sliding nightmare
        if (note.Slide == Slide.Shift || note.Slide == Slide.Downwards || note.Slide == Slide.Upwards)
        {
            Events.SuspenseValidation = true;
            var shiftBuffer = new Events();

            var targetPitch = note.GetSlideTargetPitch();
            var direction = targetPitch < note.NoteNumber ? -1 : 1;
            var semitoneDistance = Math.Abs(targetPitch - note.NoteNumber);

            if (semitoneDistance <= 1)
            {
                var slideDuration = actualDuration % 960 == 0
                    ? new Time(960)
                    : actualDuration / 4;

                currentCursor += actualDuration - slideDuration;
                actualDuration = slideDuration;
                AddLegatoPitchBends(currentCursor, note, shiftBuffer, actualDuration);
            }
            else
            {
                if (note.Beat.Index == 11 && note.Measure.Index == 72 && note.Part.Index == 8)
                {

                }

                var totalSteps = semitoneDistance - 1;
                var stepSize = note.GetShiftStepSizeTicks();

                var firstNoteDuration = actualDuration - (totalSteps * stepSize);

                if (note.Slide == Slide.Upwards || (note.tie && (note.Slide == Slide.Downwards)))
                {
                    currentCursor -= stepSize;
                }

                currentCursor += firstNoteDuration;

                var currentNote = note.NoteNumber;
                var nextNote = currentNote + direction;

                shiftBuffer.Add(new PitchBendEvent(8192), currentCursor, note);
                shiftBuffer.Add(new NoteOnEvent(nextNote.To7(), Velocity), currentCursor, note);
                if (note.tie)
                {
                    // CASE A: TIED SLIDE OUT (Up/Down) -> "Ghosting"
                    // Logic: Do NOT turn off the source note. It stays alive until the measure/beat ends.
                    if (note.Slide == Slide.Downwards || note.Slide == Slide.Upwards)
                    {
                        currentCursor += stepSize;
                    }
                    // CASE B: TIED SHIFT -> "Overlap"
                    // Logic: Advance time first, then kill (smooth transition).
                    else
                    {
                        currentCursor += stepSize;
                        shiftBuffer.Add(new NoteOffEvent(currentNote, Velocity), currentCursor, note);
                    }
                }
                else
                {
                    // CASE C: NO TIE (Any Slide) -> "Swap"
                    // Logic: Kill immediately, then advance (clean cut).
                    shiftBuffer.Add(new NoteOffEvent(currentNote, Velocity), currentCursor, note);
                    currentCursor += stepSize;
                }


                var steps = (note.Slide == Slide.Downwards || note.Slide == Slide.Upwards) ? totalSteps + 1 : totalSteps;
                for (var i = 1; i < steps; i++)
                {
                    shiftBuffer.Add(new PitchBendEvent(8192), currentCursor, note);

                    currentNote = (note.NoteNumber + i * direction).To7();
                    nextNote = (currentNote + direction).To7();

                    shiftBuffer.Add(new NoteOffEvent(currentNote, Velocity), currentCursor, note);
                    shiftBuffer.Add(new NoteOnEvent(nextNote.To7(), Velocity), currentCursor, note);

                    currentCursor += stepSize;

                }
            }

            Events.SuspenseValidation = false;
            if (note.Beat.notes.Length == 1)
            {
                var osk = 0;
                foreach (var bufferEvent in shiftBuffer)
                {
                    events.Add(bufferEvent.Event, new Time(bufferEvent.Time), note.Beat.notes[0]);
                    osk++;
                }
            }
            else
            {

                var chunks = shiftBuffer.Chunk(3).ToList();

                var strumBase = -(123 * note.Beat.notes.Length);
                var stepStrum = 123 / 2;
                var strumDecay = 10;

                for (var stepIndex = 0; stepIndex < semitoneDistance - 1; stepIndex++)
                {
                    for (var noteIndex = 0; noteIndex < note.Beat.notes.Length; noteIndex++)
                    {
                        var pitchOffset = note.Beat.notes[noteIndex].NoteNumber - note.Beat.notes[0].NoteNumber;

                        foreach (var stepEvent in chunks[stepIndex])
                        {
                            var strumEvent = stepEvent.Event.Clone();
                            if (strumEvent is NoteEvent ne)
                            {
                                ne.NoteNumber += pitchOffset.To7();
                            }

                            var strumTime = stepEvent.Time + strumBase + noteIndex * (stepStrum - (strumDecay - 1) * stepIndex);
                            events.Add(strumEvent, new Time(strumTime), note.Beat.notes[noteIndex]);
                        }
                    }
                }
            }
        }


        // Legato

        currentCursor = AddLegato(events, note, currentCursor);

        return currentCursor;
    }

    public static Time AddLegato(Events events, Nóta note, Time cursor)
    {
        if (note.Slide != Slide.Legato) return cursor;

        var actualDuration = note.ActualDuration;

        if (!note.tie && note.Slide == Slide.Legato)
        {
            if (!note.vibrato)
            {
                actualDuration /= 2;
                cursor += actualDuration;
            }

            AddLegatoPitchBends(cursor, note, events, actualDuration);
        }

        return cursor;
    }

    public static Time AddVibrato(Events events, Nóta note, Time cursor)
    {
        if (!note.vibrato) return cursor;

        events.Add(new ControlChangeEvent(1.To7(), 64.To7()), cursor, note);

        cursor += note.Slide != Slide.None
            ? note.ActualDuration / 2
            : note.ActualDuration;

        var isCleanSlide = note.Slide == Slide.Shift || note.Slide == Slide.Legato;
        if (isCleanSlide)
        {
            events.Add(new ControlChangeEvent(1.To7(), 0.To7()), cursor, note);
        }
        else
        {
            note.PendingEvents.Enqueue(new(new ControlChangeEvent(1.To7(), 0.To7()), cursor));
        }

        return cursor;
    }

    

    public static Time AddAttackNote(Events events, Nóta note, Time cursor)
    {
        if (note.Part.IsPianoLike) // for piano its different
        {
            foreach (var n in note.Beat.notes.Where(e => !e.tie))
            {
                events.Add(new PitchBendEvent(8192), cursor, n);
            }

            foreach (var n in note.Beat.notes.Where(e => !e.tie))
            {
                events.Add(new NoteOnEvent(n.NoteNumber, Velocity), cursor, n);
            }
        }
        else
        {
            foreach (var n in note.Beat.notes.Where(e => !e.tie))
            {
                events.Add(new PitchBendEvent(8192), cursor, n);
                events.Add(new NoteOnEvent(n.NoteNumber, Velocity), cursor, n);

                if (note.Beat.notes.Length > 1)
                {
                    cursor += 123;
                }
            }
        }

        return cursor;
    }

    public static void CloseBeat(Events events, Beat beat)
    {
        var beatStart = beat.AbsoluteBeatStartTime;
        var beatEnd = beatStart + beat.MusicalDuration;
        var beatLeftovers = events.NoteOns
            .Where(e => e.EndTick >= beatStart.Tick)
            .Where(e => e.EndTick <= beatEnd.Tick)
            .OrderByDescending(e => e.Note.tie)
            .ToList();

        while (beatLeftovers.Count > 0)
        {
            var note = beatLeftovers[0];

            var siblingNotes = events.NoteOns
                .Where(e => e.Note.Beat == note.Note.Beat)
                .OrderBy(e => !e.Note.tie)
                .ThenBy(e => e.TimedEvent.As<NoteOnEvent>().Channel)

                .ToList();

            var endsAt = siblingNotes.Min(e => e.EndTick);

            foreach (var sibling in siblingNotes)
            {
                beatLeftovers.Remove(CloseNote(events, sibling, endsAt));
            }
        }
    }

    public static (TimedEvent TimedEvent, Nóta Note, long EndTick) CloseNote(
            Events events,
            (TimedEvent TimedEvent, Nóta Note, long EndTick) note,
            long endsAt)
    {
        while (note.Note.PendingEvents.TryDequeue(out var pendingEvent))
        {
            events.Add(pendingEvent.Event, new Time(endsAt), note.Note);
        }

        var noteNumber = note.TimedEvent.As<NoteOnEvent>().NoteNumber;
        events.Add(new NoteOffEvent(noteNumber, Velocity), new Time(endsAt), note.Note);

        return note;
    }

    public static void BuildHeader(Part part, Events events)
    {
        var timeZero = new Time();

        for (var i = 0; i < 9; i++)
        {
            // Program Change
            events.Add(new ProgramChangeEvent(part.instrumentId.To7()), timeZero, null, i, part.partId);
        }

        for (var i = 0; i < 9; i++)
        {
            // Mod Wheel Reset
            events.Add(new ControlChangeEvent(1.To7(), 0.To7()), timeZero, null, i, part.partId);
        }

        for (var i = 0; i < 9; i++)
        {
            // Pitch Bend Reset
            events.Add(new PitchBendEvent(8192), timeZero, null, i, part.partId);
        }

        for (var i = 0; i < 9; i++)
        {
            // RPN Pitch Range Setup (Your 4 events)
            events.Add(new ControlChangeEvent(101.To7(), 0.To7()), timeZero, null, i, part.partId);
            events.Add(new ControlChangeEvent(100.To7(), 0.To7()), timeZero, null, i, part.partId);
            events.Add(new ControlChangeEvent(6.To7(), 24.To7()), timeZero, null, i, part.partId);
            events.Add(new ControlChangeEvent(38.To7(), 0.To7()), timeZero, null, i, part.partId);
        }


        if (!string.IsNullOrEmpty(part.name))
        {
            events.Add(new SequenceTrackNameEvent(part.name), timeZero, null, null, part.partId);
        }

        if (!string.IsNullOrEmpty(part.instrument))
        {
            events.Add(new InstrumentNameEvent(part.instrument), timeZero,
                null, null, part.partId);
        }
    }

    public static void AddLegatoPitchBends(Time currentCursor, Nóta note, Events events, Time actualDuration)
    {
        events.Add(new PitchBendEvent(8195), currentCursor, note);

        if (note.vibrato)
        {
            var target = note.GetSlideTarget();
            var inbetweenNote = Math.Sign(target.NoteNumber - note.NoteNumber) + note.NoteNumber;

            events.Add(new NoteOnEvent(inbetweenNote.To7(), Velocity), currentCursor, note);
            events.Add(new NoteOffEvent(note.NoteNumber, Velocity), currentCursor, note);
        }
        else
        {
            for (var l = 0; l < 99; l++)
            {
                if (l == 98)
                {
                    var fillerTime = actualDuration - 7;
                    actualDuration -= fillerTime;
                    currentCursor += fillerTime;

                    events.Add(new PitchBendEvent(8888), currentCursor, note);

                }
                else
                {
                    var legatoTime = 6;
                    actualDuration -= legatoTime;
                    currentCursor += legatoTime;

                    events.Add(new PitchBendEvent(8888), currentCursor, note);
                }
            }
        }
    }

    public static TimedEvent GetLastNoteOnEvent(Events events, SevenBitNumber noteNumber)
    {
        for (var i = events.Count - 1; ; i--)
        {
            if (events[i].Event is NoteOnEvent noteOn)
            {
                return events[i];
            }
        }
    }
}