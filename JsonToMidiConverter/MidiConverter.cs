using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter;

internal static class MidiConverter
{
    private const int TicksPerQuarterNote = 15360;

    public static readonly SevenBitNumber Velocity = 9.To7();

    public static MidiFile Convert(Song song)
    {
        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote) };
        Time.Map = song.parts[0].GetTempo(midiFile);
        midiFile.ReplaceTempoMap(Time.Map);
        song.Build();

        foreach (var part in song.parts)
        {
            var events = new Events();

            AddHeader(events, part);

            if (part.partId == 10) continue;

            foreach (var measure in part.measures)
            {
                AddMeasure(events, measure);

                foreach (var beat in measure.Beats)
                {
                    var cursor = beat.AbsoluteBeatStartTime;

                    foreach (var note in beat.notes.Take(1))
                    {
                        if (note.rest)
                        {
                            cursor += beat.MusicalDuration;
                            continue;
                        }

                        cursor = AddAttackNote(events, note, cursor);
                        cursor = AddVibrato(events, note, cursor);
                        cursor = AddSlide(events, note, cursor);
                        cursor += 123;
                    }

                    CloseBeat(events, beat);


                }
            }

            midiFile.Chunks.Add(events.ToTrackChunk());
        }

        return midiFile;
    }

    public static Time AddSlide(Events events, Nóta note, Time cursor)
    {
        if (note.Slide == Slide.None) return cursor;

        cursor = AddLegato(events, note, cursor);
        cursor = AddShift(events, note, cursor);

        return cursor;
    }

    public static Time AddShift(Events events, Nóta note, Time cursor)
    {
        if (note.Slide == Slide.None || note.Slide == Slide.Legato) return cursor;

        var actualDuration = note.ActualDuration;

        Events.SuspendValidation = true;
        var template = new Events();

        var targetPitch = note.GetSlideTargetPitch();
        var direction = targetPitch < note.NoteNumber ? -1 : 1;
        var semitoneDistance = Math.Abs(targetPitch - note.NoteNumber);

        if (semitoneDistance <= 1)
        {
            var slideDuration = actualDuration % 960 == 0
                ? new Time(960)
                : actualDuration / 4;

            cursor += actualDuration - slideDuration;
            actualDuration = slideDuration;
            AddLegatoPitchBends(cursor, note, template, actualDuration);
        }
        else
        {
            var totalSteps = semitoneDistance - 1;
            var stepSize = note.GetShiftStepSizeTicks();

            var firstNoteDuration = actualDuration - (totalSteps * stepSize);

            if (note.Slide == Slide.Upwards || (note.tie && (note.Slide == Slide.Downwards)))
            {
                cursor -= stepSize;
            }

            cursor += firstNoteDuration;

            var currentNote = note.NoteNumber;
            var nextNote = currentNote + direction;

            // Hold Note
            template.Add(new PitchBendEvent(8192), cursor, note);
            template.Add(new NoteOnEvent(nextNote.To7(), Velocity), cursor, note);
            if (note.tie)
            {
                if (note.Slide == Slide.Downwards || note.Slide == Slide.Upwards)
                {
                    cursor += stepSize;
                }
                else
                {
                    cursor += stepSize;
                    template.Add(new NoteOffEvent(currentNote, Velocity), cursor, note);
                }
            }
            else
            {
                template.Add(new NoteOffEvent(currentNote, Velocity), cursor, note);
                cursor += stepSize;
            }

            // Bridge Notes
            var steps = (note.Slide == Slide.Downwards || note.Slide == Slide.Upwards) ? totalSteps + 1 : totalSteps;
            for (var i = 1; i < steps; i++)
            {
                template.Add(new PitchBendEvent(8192), cursor, note);

                currentNote = (note.NoteNumber + i * direction).To7();
                nextNote = (currentNote + direction).To7();

                template.Add(new NoteOffEvent(currentNote, Velocity), cursor, note);
                template.Add(new NoteOnEvent(nextNote.To7(), Velocity), cursor, note);

                cursor += stepSize;

            }
        }

        Events.SuspendValidation = false;
        
        EnrichTemplate(events, template, note, semitoneDistance);

        return cursor;

    }

    public static void EnrichTemplate(Events events, Events template, Nóta note, int semitoneDistance)
    {
        if (note.Beat.notes.Length == 1)
        {
            foreach (var bufferEvent in template)
            {
                events.Add(bufferEvent.Event, new Time(bufferEvent.Time), note.Beat.notes[0]);
            }
        }
        else
        {

            var chunks = template.Chunk(3).ToList();

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
                while (sibling.Note.PendingEvents.TryDequeue(out var pendingEvent))
                {
                    events.Add(pendingEvent.Event, new Time(endsAt), sibling.Note);
                }

                var noteNumber = sibling.TimedEvent.As<NoteOnEvent>().NoteNumber;
                events.Add(new NoteOffEvent(noteNumber, Velocity), new Time(endsAt), sibling.Note);
                beatLeftovers.Remove(sibling);
            }
        }
    }

    public static void AddMeasure(Events events, Measure measure)
    {
        events.Add(new MarkerEvent($"MEASURE_{measure.Index}"), measure.StartTime, null, null, measure.Part.partId);

        var measureChange = measure.Part.automations.tempo.SingleOrDefault(e => e.measure == measure.Index);
        if (measureChange != null && measure.Index != 0)
        {
            var newTempo = Tempo.FromBeatsPerMinute(measureChange.bpm).MicrosecondsPerQuarterNote;
            events.Add(new SetTempoEvent(newTempo), measure.StartTime, null, null, measure.Part.partId);
        }
    }

    public static void AddHeader(Events events, Part part)
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
}