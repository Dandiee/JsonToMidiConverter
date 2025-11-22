using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter;

internal static class MidiConverter
{
    private const int TicksPerQuarter = 15360;
    private const int TicksPer64Th = 960; // The "Magic Grid" unit
    private const int StrumOffsetTicks = 123;

    // Standard MIDI Values
    private const int PitchBendCenter = 8192;

    public static readonly SevenBitNumber DefaultVelocity = 112.To7();

    public static MidiFile Convert(Song song)
    {
        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarter) };
        Time.Map = song.parts[0].GetTempo(midiFile);
        midiFile.ReplaceTempoMap(Time.Map);
        song.Build();

        foreach (var part in song.parts)
        {
            var trackEvents = new Events();
            AddTrackHeader(trackEvents, part);

            foreach (var measure in part.measures)
            {
                AddMeasureMarker(trackEvents, measure);

                foreach (var beat in measure.Beats)
                {
                    var currentTime = beat.AbsoluteBeatStartTime;

                    // Process only the first note group (Chord/Strum)
                    foreach (var noteGroup in beat.notes.Take(1))
                    {
                        if (noteGroup.rest)
                        {
                            currentTime += beat.MusicalDuration;
                            continue;
                        }

                        currentTime = AddNoteAttack(trackEvents, noteGroup, currentTime);
                        currentTime = AddVibrato(trackEvents, noteGroup, currentTime);
                        currentTime = AddSlides(trackEvents, noteGroup, currentTime);

                        currentTime += StrumOffsetTicks;
                    }

                    CloseBeat(trackEvents, beat);
                }
            }

            midiFile.Chunks.Add(trackEvents.ToTrackChunk());
        }

        return midiFile;
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
                events.Add(new NoteOffEvent(noteNumber, DefaultVelocity), new Time(endsAt), sibling.Note);
                beatLeftovers.Remove(sibling);
            }
        }
    }

    public static Time AddSlides(Events events, Nóta note, Time currentTime)
    {
        if (note.Slide == Slide.None) return currentTime;

        // Note: AddShift handles "Shift", "Downwards", and "Upwards"
        currentTime = AddLegato(events, note, currentTime);
        currentTime = AddShift(events, note, currentTime);

        return currentTime;
    }

    public static Time AddShift(Events events, Nóta note, Time currentTime)
    {
        if (note.Slide == Slide.None || note.Slide == Slide.Legato) return currentTime;

        var fullDuration = note.ActualDuration;

        // Temporarily disable validation to allow out-of-order insertion in the template
        Events.SuspendValidation = true;
        var slideTemplate = new Events();

        var targetPitch = note.GetSlideTargetPitch();
        var direction = targetPitch < note.NoteNumber ? -1 : 1;
        var semitoneDistance = Math.Abs(targetPitch - note.NoteNumber);


        // --- CASE 1: CONTINUOUS SLIDE (1 Semitone or Legato Logic) ---
        if (semitoneDistance <= 1)
        {
            // "Magic Grid" vs "Ratio" Logic
            var slideTailDuration = fullDuration % TicksPer64Th == 0
                ? new Time(TicksPer64Th)  // Grid Aligned
                : fullDuration / 4;            // Ratio (Tuplet) Aligned

            // Advance time to the start of the slide (Hold Phase)
            currentTime += fullDuration - slideTailDuration;
            fullDuration = slideTailDuration;

            // Generate the Pitch Bend Ramp
            AddLegatoPitchBends(currentTime, note, slideTemplate, fullDuration);
        }
        else // --- CASE 2: STEPPED SLIDE (> 1 Semitone) ---
        {
            var totalSteps = semitoneDistance - 1;
            var stepSizeTicks = note.GetShiftStepSizeTicks(); // Assuming this uses our Unified Logic

            var firstNoteHoldDuration = fullDuration - (totalSteps * stepSizeTicks);

            // Adjustment for specific slide directions (Slide Out logic)
            if (note.Slide == Slide.Upwards || (note.tie && (note.Slide == Slide.Downwards)))
            {
                currentTime -= stepSizeTicks;
            }

            currentTime += firstNoteHoldDuration;

            var currentNoteNum = note.NoteNumber;
            var nextNoteNum = currentNoteNum + direction;

            // -- FIRST STEP --
            slideTemplate.Add(new PitchBendEvent(PitchBendCenter), currentTime, note);
            slideTemplate.Add(new NoteOnEvent(nextNoteNum.To7(), DefaultVelocity), currentTime, note);

            // Handle Tie Logic (Ghost Note vs Swap)
            if (note.tie)
            {
                if (note.Slide == Slide.Downwards || note.Slide == Slide.Upwards)
                {
                    // Ghost Note: Source stays alive
                    currentTime += stepSizeTicks;
                }
                else
                {
                    // Standard Tie: Overlap
                    currentTime += stepSizeTicks;
                    slideTemplate.Add(new NoteOffEvent(currentNoteNum, DefaultVelocity), currentTime, note);
                }
            }
            else
            {
                // No Tie: Clean Swap
                slideTemplate.Add(new NoteOffEvent(currentNoteNum, DefaultVelocity), currentTime, note);
                currentTime += stepSizeTicks;
            }

            var loopCount = (note.Slide == Slide.Downwards || note.Slide == Slide.Upwards)
                ? totalSteps + 1
                : totalSteps;

            for (var i = 1; i < loopCount; i++)
            {
                slideTemplate.Add(new PitchBendEvent(PitchBendCenter), currentTime, note);

                currentNoteNum = (note.NoteNumber + i * direction).To7();
                nextNoteNum = (currentNoteNum + direction).To7();

                slideTemplate.Add(new NoteOffEvent(currentNoteNum, DefaultVelocity), currentTime, note);
                slideTemplate.Add(new NoteOnEvent(nextNoteNum.To7(), DefaultVelocity), currentTime, note);

                currentTime += stepSizeTicks;
            }
        }

        Events.SuspendValidation = false;

        // Apply the template to all strings in the chord (Strumming simulation)
        EnrichTemplate(events, slideTemplate, note, semitoneDistance);

        return currentTime;
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
            // Chord: Apply "Dynamic Strum Convergence" logic
            var chunks = template.Chunk(3).ToList(); // Assuming 3 events per step (PB, Off, On)

            var strumBaseOffset = -(StrumOffsetTicks * note.Beat.notes.Length);
            var stepStrumDelta = StrumOffsetTicks / 2;
            var strumDecayRate = 10;

            for (var stepIndex = 0; stepIndex < semitoneDistance - 1; stepIndex++)
            {
                for (var noteIndex = 0; noteIndex < note.Beat.notes.Length; noteIndex++)
                {
                    var pitchOffset = note.Beat.notes[noteIndex].NoteNumber - note.Beat.notes[0].NoteNumber;

                    foreach (var stepEvent in chunks[stepIndex])
                    {
                        var clonedEvent = stepEvent.Event.Clone();
                        if (clonedEvent is NoteEvent ne)
                        {
                            ne.NoteNumber += pitchOffset.To7();
                        }

                        // Calculate the tightening strum timing
                        var dynamicStrumOffset = stepStrumDelta - (strumDecayRate - 1) * stepIndex;
                        var strumTime = stepEvent.Time + strumBaseOffset + noteIndex * dynamicStrumOffset;

                        events.Add(clonedEvent, new Time(strumTime), note.Beat.notes[noteIndex]);
                    }
                }
            }
        }
    }

    public static Time AddLegato(Events events, Nóta note, Time currentTime)
    {
        if (note.Slide != Slide.Legato) return currentTime;

        var remainingDuration = note.ActualDuration;

        if (!note.tie && note.Slide == Slide.Legato)
        {
            if (!note.vibrato)
            {
                // Split note 50/50 if no vibrato
                remainingDuration /= 2;
                currentTime += remainingDuration;
            }

            AddLegatoPitchBends(currentTime, note, events, remainingDuration);
        }

        return currentTime;
    }

    public static Time AddVibrato(Events events, Nóta note, Time currentTime)
    {
        if (!note.vibrato) return currentTime;

        // Vibrato On (Depth 64)
        events.Add(new ControlChangeEvent(1.To7(), 64.To7()), currentTime, note);

        // Determine where to turn it off
        currentTime += note.Slide != Slide.None
            ? note.ActualDuration / 2 // Clean Slide: Stop halfway
            : note.ActualDuration;    // Sustain/SlideOut: Stop at end

        var isCleanSlide = note.Slide == Slide.Shift || note.Slide == Slide.Legato;
        if (isCleanSlide)
        {
            events.Add(new ControlChangeEvent(1.To7(), 0.To7()), currentTime, note);
        }
        else
        {
            // This ensures it happens after the pitch bends in the file
            note.PendingEvents.Enqueue(new(new ControlChangeEvent(1.To7(), 0.To7()), currentTime));
        }

        return currentTime;
    }

    public static Time AddNoteAttack(Events events, Nóta note, Time currentTime)
    {
        if (note.Part.IsPianoLike)
        {
            // Pianos don't strum, they hit simultaneously
            foreach (var n in note.Beat.notes.Where(e => !e.tie))
            {
                events.Add(new PitchBendEvent(PitchBendCenter), currentTime, n);
            }
            foreach (var n in note.Beat.notes.Where(e => !e.tie))
            {
                events.Add(new NoteOnEvent(n.NoteNumber, DefaultVelocity), currentTime, n);
            }
        }
        else
        {
            // Guitars Strum (Offset by 123 ticks)
            foreach (var n in note.Beat.notes.Where(e => !e.tie))
            {
                events.Add(new PitchBendEvent(PitchBendCenter), currentTime, n);
                events.Add(new NoteOnEvent(n.NoteNumber, DefaultVelocity), currentTime, n);

                if (note.Beat.notes.Length > 1)
                {
                    currentTime += StrumOffsetTicks;
                }
            }
        }

        return currentTime;
    }

    public static void AddLegatoPitchBends(Time currentTime, Nóta note, Events events, Time durationOfSlide)
    {
        events.Add(new PitchBendEvent(8195), currentTime, note);

        if (note.vibrato)
        {
            // If Vibrato is active, we simulate the transition with a discreet note
            // instead of a bend, to avoid conflict? (Check logic here based on M47)
            var target = note.GetSlideTarget();
            var direction = Math.Sign(target.NoteNumber - note.NoteNumber);
            var inbetweenNote = note.NoteNumber + direction;

            events.Add(new NoteOnEvent(inbetweenNote.To7(), DefaultVelocity), currentTime, note);
            events.Add(new NoteOffEvent(note.NoteNumber, DefaultVelocity), currentTime, note);
        }
        else
        {
            // Interpolate Pitch Bends distributed over 'durationOfSlide'
            int interpolationSteps = 99;

            for (var i = 0; i < interpolationSteps; i++)
            {
                // Calculate time delta for this micro-step
                // Last step takes the remainder to ensure perfect timing
                var isLastStep = i == interpolationSteps - 1;

                if (isLastStep)
                {
                    // Final Cleanup Step (7 ticks gap?)
                    var fillerTime = durationOfSlide - 7;
                    durationOfSlide -= fillerTime;
                    currentTime += fillerTime;

                    // 8888 seems to be a placeholder for "Calculate correct Pitch Value later"?
                    events.Add(new PitchBendEvent(8888), currentTime, note);
                }
                else
                {
                    var microStepTime = 6; // 6-7 ticks per bend event
                    durationOfSlide -= microStepTime;
                    currentTime += microStepTime;

                    events.Add(new PitchBendEvent(8888), currentTime, note);
                }
            }
        }
    }

    public static void AddMeasureMarker(Events events, Measure measure)
    {
        events.Add(new MarkerEvent($"MEASURE_{measure.Index}"), measure.StartTime, null, null, measure.Part.partId);

        var measureChange = measure.Part.automations.tempo.SingleOrDefault(e => e.measure == measure.Index);
        if (measureChange != null && measure.Index != 0)
        {
            var newTempo = Tempo.FromBeatsPerMinute(measureChange.bpm).MicrosecondsPerQuarterNote;
            events.Add(new SetTempoEvent(newTempo), measure.StartTime, null, null, measure.Part.partId);
        }
    }

    public static void AddTrackHeader(Events events, Part part)
    {
        var timeZero = new Time();

        var channels = part.instrumentId == 1024
            ? [9]
            : Enumerable.Range(0, 9).ToArray();

        foreach(var i in channels)
        {
            // Program Change
            events.Add(new ProgramChangeEvent(part.instrumentId.To7()), timeZero, null, i, part.partId);
        }

        foreach (var i in channels)
        {
            // Mod Wheel Reset
            events.Add(new ControlChangeEvent(1.To7(), 0.To7()), timeZero, null, i, part.partId);
        }

        foreach (var i in channels)
        {
            // Pitch Bend Reset
            events.Add(new PitchBendEvent(8192), timeZero, null, i, part.partId);
        }

        foreach (var i in channels)
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

   
}