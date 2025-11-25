using JsonToMidiConverter.Context;
using JsonToMidiConverter.Models.Song;
using JsonToMidiConverter.Test;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Microsoft.VisualBasic;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Xml.Linq;
using Slide = JsonToMidiConverter.Context.Slide;

namespace JsonToMidiConverter;

internal static class Converter
{
    private const int TicksPerQuarter = 15360;
    private const int TicksPer64Th = 960; // The "Magic Grid" unit
    private const int StrumOffsetTicks = 123;

    // Standard MIDI Values
    private const ushort PitchBendCenter = 8192;

    public static readonly SevenBitNumber DefaultVelocity = 112.To7();
    public static List<(long AbsoluteTime, MidiEvent Event)>[] ReferenceData;
    public static readonly long StandardSlideStepSize = 960;

    public static MidiFile Convert(Song song, string referenceMidiPath)
    {
        ReferenceData = GetReferenceMidiData(referenceMidiPath);
        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarter) };
        Time.Map = song.Parts[0].GetTempo(midiFile);
        midiFile.ReplaceTempoMap(Time.Map);
        song.Build();

        foreach (var part in song.Parts)
        {
            var events = new Events();
            AddTrackHeader(events, part);

            foreach (var measure in part.Measures)
            {
                AddMeasureMarker(events, measure);

                foreach (var beat in measure.Beats)
                {
                    foreach (var note in beat.Notes.Where(e => !e.Rest))
                    {
                        if (note.Is("N0 B6 M55 P8"))
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
                                On(events, note, note.NoteNumber, note.GetStartTime(), note.GetEndTime());

                                end = start + note.ActualDuration;
                                var step = note.ActualDuration / 2d / 100d;
                                events.Add(new PitchBendEvent(PitchBendCenter), end - 960, note);
                                Enumerable.Range(1, 100).ToList().ForEach(i =>
                                {
                                    events.Add(new PitchBendEvent(PitchBendCenter), end - 960 + (i * step.Tick), note);
                                });
                            }
                            else
                            {
                                var holdFrom = start;
                                var holdTo = holdFrom + slide.HoldDuration - (note.Index * 62);

                                On(events, note, note.NoteNumber, holdFrom, holdTo);

                                for (var i = 0; i < slide.Steps; i++)
                                {
                                    var stepFrom = holdTo + slide.StepDuration * i - (i * 9 * note.Index);
                                    var stepTo = stepFrom + slide.StepDuration - 9 * note.Index;
                                    var stepNote = note.NoteNumber + slide.Direction * (i + 1);

                                    On(events, note, stepNote, stepFrom, stepTo);
                                }
                            }
                        }
                        else
                        {
                            On(events, note, note.NoteNumber, note.GetStartTime(), note.GetEndTime());
                        }


                    }
                }
            }

            Validate(events, part);
            midiFile.Chunks.Add(events.ToTrackChunk());
        }

        return midiFile;
    }

    public static void On(Events events, Nóta note, int noteNumber, Time from, Time to)
    {
        events.Add(new PitchBendEvent(PitchBendCenter), from, note);
        events.Add(new NoteOnEvent((SevenBitNumber)noteNumber, DefaultVelocity), from, note);
        events.Add(new NoteOffEvent((SevenBitNumber)noteNumber, DefaultVelocity), to, note);
    }

    public static void Validate(Events events, Part part)
    {
        var chunk = ReferenceData[part.PartId];

        var isStarted = false;
        for (var i = 0; i < chunk.Count; i++)
        {
            var referenceEvent = chunk[i];
            isStarted |= referenceEvent.Event.Is<MarkerEvent>();
            
            if (!isStarted) continue;
            if (referenceEvent.Event.DeltaTime < 11 && referenceEvent.Event.Is<PitchBendEvent>()) continue;
            if (referenceEvent.Event is MarkerEvent mark && mark.Text == "END_OF_VOICE") break;
            if (referenceEvent.Event.EventType == MidiEventType.PitchBend)
            {
                var pitchBendEventsCount = 0;
                for (; chunk[i + pitchBendEventsCount].Event.EventType == MidiEventType.PitchBend; pitchBendEventsCount++) ;
                if (pitchBendEventsCount > 10)
                {
                    i += pitchBendEventsCount - 1;
                    continue;
                }
            }

            var cursor = i + 2;
            var time = referenceEvent.AbsoluteTime;
            var type = referenceEvent.Event.EventType;
            var partDetails = $"P{part.Index} {part.Name} - {part.Instrument}";

            var matchesByTime = events.Where(e => Math.Abs(e.Time - referenceEvent.AbsoluteTime) < 10).Where(e => e.Event.EventType == referenceEvent.Event.EventType).ToList();
            var closest = events.Where(e => e.Event.EventType == referenceEvent.Event.EventType).MinBy(e => Math.Abs(e.Time - referenceEvent.AbsoluteTime));
            var distance = closest.Time - referenceEvent.AbsoluteTime;

            if (matchesByTime.Count > 1)
            {
                if (referenceEvent.Event.Is<ChannelEvent>()) matchesByTime = matchesByTime.Where(e => e.Event.As<ChannelEvent>().Channel == referenceEvent.Event.As<ChannelEvent>().Channel).ToList();
                if (referenceEvent.Event.Is<NoteEvent>()) matchesByTime = matchesByTime.Where(e => e.Event.As<NoteEvent>().NoteNumber == referenceEvent.Event.As<NoteEvent>().NoteNumber).ToList();
                if (referenceEvent.Event.Is<ControlChangeEvent>()) matchesByTime = matchesByTime.Where(e => e.Event.As<ControlChangeEvent>().ControlValue == referenceEvent.Event.As<ControlChangeEvent>().ControlValue).ToList();
            }

            var match = matchesByTime.Single();

            if (match.Is<NoteEvent>())
            {
                Debug.Assert(match.As<NoteEvent>().NoteNumber == referenceEvent.Event.As<NoteEvent>().NoteNumber);
            }

            if (match.Is<ChannelEvent>())
            {
                Debug.Assert(match.As<ChannelEvent>().Channel == referenceEvent.Event.As<ChannelEvent>().Channel);
            }

            if (match.Is<ControlChangeEvent>())
            {
                Debug.Assert(match.As<ControlChangeEvent>().ControlNumber == referenceEvent.Event.As<ControlChangeEvent>().ControlNumber);
                Debug.Assert(match.As<ControlChangeEvent>().ControlValue == referenceEvent.Event.As<ControlChangeEvent>().ControlValue);
            }

            if (match.Is<TextEvent>())
            {
                Debug.Assert(match.As<TextEvent>().Text == referenceEvent.Event.As<TextEvent>().Text);
            }

            if (match.Is<SetTempoEvent>())
            {
                Debug.Assert(match.As<SetTempoEvent>().MicrosecondsPerQuarterNote == referenceEvent.Event.As<SetTempoEvent>().MicrosecondsPerQuarterNote);
            }
        }
    }


    public static void CloseBeat(Events events, Beat beat)
    {
        if (beat.Is("B5 M72 P5"))
        {

        }

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

    public static Time AddSlide(Events events, Nóta note, Time currentTime)
    {
        if (note.Slide == Slide.None) return currentTime;



        Events.SuspendValidation = note.Beat.IsAccord;
        var buffer = note.Beat.IsAccord
            ? new Events()
            : events;

        var slide = note.GetSlide();

        if (note.Is("N0 B5 M47 P8"))
        {

        }

        // --- CASE 1: CONTINUOUS SLIDE (1 Semitone or Legato Logic) ---
        if (!slide.IsStepped)
        {
            AddLegatoPitchBends(buffer, note);
        }
        else // --- CASE 2: STEPPED SLIDE (> 1 Semitone) ---
        {
            Debug.WriteLine($"P{note.Part.Index}, M{note.Measure.Index}, B{note.Beat.Index}, N{note.Index} S{note.Slide} T{note.Tie} V{note.Vibrato}: SteppedSlide");

            currentTime = note.Beat.AbsoluteBeatStartTime;
            currentTime += slide.HoldDuration;

            for (var i = 0; i < slide.Steps; i++)
            {
                buffer.Add(new PitchBendEvent(PitchBendCenter), currentTime, note);

                var noteToTurnOff = (note.NoteNumber + i * slide.Direction).To7(); // previous note
                var noteToTurnOn = (noteToTurnOff + slide.Direction).To7();        // next note

                if (i == 0) // First we turn on the new note and turn off the hold note
                {
                    buffer.Add(new NoteOnEvent(noteToTurnOn, DefaultVelocity), currentTime, note);

                    if (note.Slide != Slide.Downwards)
                    {
                        if (note.Tie && note.Slide != Slide.Upwards)
                        {
                            currentTime += slide.StepDuration;

                        }

                        if (!(note.Slide == Slide.Upwards && note.Tie))
                        {
                            buffer.Add(new NoteOffEvent(noteToTurnOff, DefaultVelocity), currentTime, note);
                        }


                    }
                }
                else // Then we swap between the sliding notes
                {
                    buffer.Add(new NoteOffEvent(noteToTurnOff, DefaultVelocity), currentTime, note);
                    buffer.Add(new NoteOnEvent(noteToTurnOn, DefaultVelocity), currentTime, note);
                }

                currentTime += slide.StepDuration;
            }
        }

        Events.SuspendValidation = false;

        // Apply the template to all strings in the chord (Strumming simulation)
        if (note.Beat.IsAccord)
        {
            EnrichTemplate(events, buffer, note, slide);
        }

        return currentTime;
    }

    public static void EnrichTemplate(Events events, Events template, Nóta note, JsonToMidiConverter.Models.Song.Slide slide)
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

            for (var stepIndex = 0; stepIndex < slide.Steps; stepIndex++)
            {
                for (var noteIndex = 0; noteIndex < note.Beat.Notes.Length; noteIndex++)
                {
                    foreach (var stepEvent in chunks[stepIndex])
                    {
                        var clonedEvent = stepEvent.Event.Clone();
                        if (clonedEvent is NoteEvent ne)
                        {
                            ne.NoteNumber += (note.Beat.Notes[noteIndex].NoteNumber - note.Beat.Notes[0].NoteNumber).To7();
                        }

                        var strum = noteIndex == 0
                            ? 0
                            : (StrumOffsetTicks / 2 - stepIndex * 9) * noteIndex;

                        var time = stepEvent.Time + strum;
                        events.Add(clonedEvent, new Time(time), note.Beat.Notes[noteIndex]);
                    }
                }
            }
        }
    }

    //public static Time AddLegato(Events events, Nóta note, Time currentTime)
    //{
    //    if (note.Slide != Slide.Legato && note.Bend == null) return currentTime;

    //    var remainingDuration = note.ActualDuration;

    //    if (!note.Tie && note.Slide == Slide.Legato)
    //    {
    //        Debug.WriteLine($"");

    //        if (!note.Vibrato)
    //        {
    //            Debug.WriteLine($"P{note.Part.Index}, M{note.Measure.Index}, B{note.Beat.Index}, N{note.Index} S{note.Slide} T{note.Tie} V{note.Vibrato}: PitchBends");

    //            // Split note 50/50 if no vibrato
    //            remainingDuration /= 2;
    //            currentTime += remainingDuration;
    //        }
    //        else
    //        {
    //            Debug.WriteLine($"P{note.Part.Index}, M{note.Measure.Index}, B{note.Beat.Index}, N{note.Index} S{note.Slide} T{note.Tie} V{note.Vibrato}: PitchBends");
    //        }

    //        AddLegatoPitchBends(events, note);
    //    }

    //    return currentTime;
    //}

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
        if (note.Is("N0 B6 M55 P8"))
        {

        }

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

                if (note.Beat.Notes.Length > 1 && n != note.Beat.Notes[^1])
                {
                    currentTime += StrumOffsetTicks;
                }
            }
        }

        return currentTime;
    }

    public static void AddLegatoPitchBends(Events events, Nóta note)
    {
        var fullDuration = note.ActualDuration;
        // "Magic Grid" vs "Ratio" Logic
        var slideTailDuration = fullDuration % TicksPer64Th == 0
            ? new Time(TicksPer64Th)  // Grid Aligned
            : fullDuration / 4;            // Ratio (Tuplet) Aligned

        // Advance time to the start of the slide (Hold Phase)
        var currentTime = note.Beat.AbsoluteBeatStartTime;
        currentTime += fullDuration - slideTailDuration;
        fullDuration = slideTailDuration;

        //currentTime = note.Beat.AbsoluteBeatStartTime;
        //currentTime += 960;

        if (note.Slide == Slide.None && note.Bend != null)
        {
            currentTime = note.Beat.AbsoluteBeatStartTime + note.ActualDuration * 0.05;
        }

        events.Add(new PitchBendEvent(8195), currentTime, note);

        if (note.Vibrato && note.Bend == null)
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
                    var fillerTime = fullDuration - 7;
                    fullDuration -= fillerTime;
                    currentTime += fillerTime;

                    // 8888 seems to be a placeholder for "Calculate correct Pitch Value later"?
                    events.Add(new PitchBendEvent(8888), currentTime, note);
                }
                else
                {
                    var microStepTime = 6; // 6-7 ticks per bend event
                    fullDuration -= microStepTime;
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