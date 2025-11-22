using JsonToMidiConverter.Context;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter;

internal static class Converter
{
    private const int TicksPerQuarter = 15360;
    private const int TicksPer64Th = 960; // The "Magic Grid" unit
    private const int StrumOffsetTicks = 123;

    // Standard MIDI Values
    private const int PitchBendCenter = 8192;

    public static readonly SevenBitNumber DefaultVelocity = 112.To7();
    public static List<(long AbsoluteTime, MidiEvent Event)>[] ReferenceData;

    public static MidiFile Convert(Song song, string referenceMidiPath)
    {
        ReferenceData = GetReferenceMidiData(referenceMidiPath);
        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarter) };
        Time.Map = song.Parts[0].GetTempo(midiFile);
        midiFile.ReplaceTempoMap(Time.Map);
        song.Build();

        foreach (var part in song.Parts)
        {
            var trackEvents = new Events();
            AddTrackHeader(trackEvents, part);

            foreach (var measure in part.Measures)
            {
                AddMeasureMarker(trackEvents, measure);

                foreach (var beat in measure.Beats)
                {
                    var currentTime = beat.AbsoluteBeatStartTime;

                    // Process only the first note group (Chord/Strum)
                    foreach (var noteGroup in beat.Notes.Take(1))
                    {
                        if (noteGroup.Rest)
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
            .OrderByDescending(e => e.Note.Tie)
            .ToList();

        while (beatLeftovers.Count > 0)
        {
            var note = beatLeftovers[0];

            var siblingNotes = events.NoteOns
                .Where(e => e.Note.Beat == note.Note.Beat)
                .OrderBy(e => !e.Note.Tie)
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
        currentTime = AddShift(events, note, currentTime);
        currentTime = AddLegato(events, note, currentTime);
        

        return currentTime;
    }

    public static Time AddShift(Events events, Nóta note, Time currentTime)
    {
        if (note.Slide == Slide.None) return currentTime;

        


        var fullDuration = note.ActualDuration;

        // Temporarily disable validation to allow out-of-order insertion in the template
        Events.SuspendValidation = true;
        var slideTemplate = new Events();

        var targetPitch = note.GetSlideTargetPitch();
        var direction = targetPitch < note.NoteNumber ? -1 : 1;
        var semitoneDistance = Math.Abs(targetPitch - note.NoteNumber);

        if (note.Slide == Slide.Legato)
        {
            if (semitoneDistance == 1)
            {
                return currentTime;
            }
        }

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
            if (note.Slide == Slide.Upwards || (note.Tie && (note.Slide == Slide.Downwards)))
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
            if (note.Tie)
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
        if (note.Beat.Notes.Length == 1)
        {
            foreach (var bufferEvent in template)
            {
                events.Add(bufferEvent.Event, new Time(bufferEvent.Time), note.Beat.Notes[0]);
            }
        }
        else
        {
            // Chord: Apply "Dynamic Strum Convergence" logic
            var chunks = template.Chunk(3).ToList(); // Assuming 3 events per step (PB, Off, On)

            var strumBaseOffset = -(StrumOffsetTicks * note.Beat.Notes.Length);
            var stepStrumDelta = StrumOffsetTicks / 2;
            var strumDecayRate = 10;

            for (var stepIndex = 0; stepIndex < semitoneDistance - 1; stepIndex++)
            {
                for (var noteIndex = 0; noteIndex < note.Beat.Notes.Length; noteIndex++)
                {
                    var pitchOffset = note.Beat.Notes[noteIndex].NoteNumber - note.Beat.Notes[0].NoteNumber;

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

                        events.Add(clonedEvent, new Time(strumTime), note.Beat.Notes[noteIndex]);
                    }
                }
            }
        }
    }

    public static Time AddLegato(Events events, Nóta note, Time currentTime)
    {
        // 1. Guard Clauses
        if (note.Slide != Slide.Legato) return currentTime;

        // We only process if it's NOT a tie (Ties usually just extend the previous note)
        if (!note.Tie)
        {
            note.Is(0,4,11,0);
            // 2. Calculate the Unified Slide Duration
            // This is the single source of truth now.
            var slideDurationTicks = note.NewGetSlideDurationTicks();

            // 3. Handle The "Hold" Phase
            // If there's no vibrato, we need to advance the cursor past the "Hold" part.
            // If there IS vibrato, we assume the vibrato logic handles the timing or starts immediately.
            if (!note.Vibrato)
            {
                // Hold Duration = Total - Slide
                // We advance 'currentTime' to the exact moment the slide starts.
                currentTime += (note.ActualDuration.Tick - slideDurationTicks);
            }

            // 4. Generate the Bends
            // Pass the calculated 'slideDurationTicks' explicitly.
            // (Assuming AddLegatoPitchBends takes a Time object or long)
            AddLegatoPitchBends(currentTime, note, events, new Time(slideDurationTicks));
        }

        return currentTime;
    }

    public static Time AddVibrato(Events events, Nóta note, Time currentTime)
    {
        if (!note.Vibrato) return currentTime;

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
            foreach (var n in note.Beat.Notes.Where(e => !e.Tie))
            {
                events.Add(new PitchBendEvent(PitchBendCenter), currentTime, n);
            }
            foreach (var n in note.Beat.Notes.Where(e => !e.Tie))
            {
                events.Add(new NoteOnEvent(n.NoteNumber, DefaultVelocity), currentTime, n);
            }
        }
        else
        {
            // Guitars Strum (Offset by 123 ticks)
            foreach (var n in note.Beat.Notes.Where(e => !e.Tie))
            {
                events.Add(new PitchBendEvent(PitchBendCenter), currentTime, n);
                events.Add(new NoteOnEvent(n.NoteNumber, DefaultVelocity), currentTime, n);

                if (note.Beat.Notes.Length > 1)
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

        if (note.Vibrato)
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
        events.Add(new MarkerEvent($"MEASURE_{measure.Index}"), measure.StartTime, null, null, measure.Part.PartId);

        var measureChange = measure.Part.Automations.Tempo.SingleOrDefault(e => e.Measure == measure.Index);
        if (measureChange != null && measure.Index != 0)
        {
            var newTempo = Tempo.FromBeatsPerMinute(measureChange.Bpm).MicrosecondsPerQuarterNote;
            events.Add(new SetTempoEvent(newTempo), measure.StartTime, null, null, measure.Part.PartId);
        }
    }

    public static void AddTrackHeader(Events events, Part part)
    {
        var timeZero = new Time();

        var channels = part.InstrumentId == 1024
            ? [9]
            : Enumerable.Range(0, 9).ToArray();

        foreach(var i in channels)
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

    private static List<(long AbsoluteTime, MidiEvent Event)>[] GetReferenceMidiData(string referenceMidiFile)
    {
        var referenceMidi = MidiFile.Read(referenceMidiFile);
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