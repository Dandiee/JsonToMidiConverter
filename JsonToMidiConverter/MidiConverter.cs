using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Diagnostics;

namespace JsonToMidiConverter;

internal static partial class MidiConverter
{
    private const int TicksPerQuarterNote = 15360;
    public static readonly List<(long AbsoluteTime, MidiEvent Event)>[] ReferenceData = GetReferenceMidiData();
    public static bool SuspenseValidation = false;

    public static MidiFile Convert(Song song)
    {
        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote) };
        Time.Map = song.parts[0].GetTempo(midiFile); ;
        song.Build();

        foreach (var part in song.parts)
        {
            var events = new List<TimedEvent>();
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
                    var beatVelocity = (SevenBitNumber)(112);
                    var beatCursor = measureCursor + beat.GetMeasureStartDuration(Time.Map);

                    var prevBeat = beat.GetPrevious();
                    var nextBeat = beat.GetNext();

                    currentCursor = beatCursor;

                    foreach (var note in beat.notes.Take(1))
                    {
                        var rawNoteDuration = beat.MusicalDuration.Clone();

                        if (note.staccato)
                        {
                            rawNoteDuration /= 2;
                        }

                        var actualDuration = prevBeat?.graceNote == "onBeat"
                            ? rawNoteDuration - prevBeat.MusicalDuration
                            : rawNoteDuration;

                        if (note.rest)
                        {
                            currentCursor += beat.MusicalDuration;
                            continue;
                        }




                        // NoteOnEvent
                        {

                            if (part.IsPianoLike) // for piano its different
                            {
                                foreach (var n in beat.notes.Where(e => !e.tie))
                                {
                                    events.Add(new PitchBendEvent(8192), currentCursor, n);
                                }

                                foreach (var n in beat.notes.Where(e => !e.tie))
                                {
                                    note.NoteOnEvent = events.Add(new NoteOnEvent((SevenBitNumber)n.NoteNumber, beatVelocity), currentCursor, n).Event;
                                }
                            }

                            else
                            {
                                foreach (var n in beat.notes.Where(e => !e.tie))
                                {
                                    var lasstten = events.Skip(events.Count - 20).Take(20).ToList();
                                    events.Add(new PitchBendEvent(8192), currentCursor, n);

                                    note.NoteOnEvent = events.Add(new NoteOnEvent((SevenBitNumber)n.NoteNumber, beatVelocity), currentCursor, n).Event;

                                    if (beat.notes.Length > 1)
                                    {
                                        currentCursor += 123;
                                    }
                                }
                            }
                        }


                        if (note.slide == "shift" || note.slide == "downwards" || note.slide == "upwards")
                        {
                            SuspenseValidation = true;
                            var shiftBuffer = new List<TimedEvent>();

                            var targetPitch = note.GetSlideTargetPitch();
                            var direction = targetPitch < note.NoteNumber ? -1 : 1;
                            var semitoneDistance = Math.Abs(targetPitch - note.NoteNumber);

                            if (semitoneDistance <= 1)
                            {
                                var shiftOffsetDuration = 960;
                                actualDuration -= new Time(shiftOffsetDuration);
                                currentCursor += actualDuration;
                                actualDuration = new Time(shiftOffsetDuration);
                                AddLegatoPitchBends(currentCursor, note, shiftBuffer, actualDuration);
                            }
                            else
                            {
                                if (note.slide == "upwards")
                                {

                                }

                                var totalSteps = semitoneDistance - 1;
                                var stepSize = note.GetShiftStepSizeTicks();

                                var firstNoteDuration = actualDuration - (totalSteps * stepSize);

                                if (note.tie && (note.slide == "downwards" || note.slide == "upwards" ))
                                {
                                    currentCursor -= stepSize;
                                }

                                currentCursor += firstNoteDuration;

                                var currentNote = (SevenBitNumber)note.NoteNumber;
                                var nextNote = (SevenBitNumber)(currentNote + direction);

                                shiftBuffer.Add(new PitchBendEvent(8192), currentCursor, note);
                                shiftBuffer.Add(new NoteOnEvent(nextNote, (SevenBitNumber)95), currentCursor, note);

                                if (note.slide == "shift")
                                {
                                    if (note.tie)
                                    {
                                        currentCursor += stepSize;
                                        shiftBuffer.Add(new NoteOffEvent(currentNote, beatVelocity), currentCursor, note);
                                    }
                                    else
                                    {
                                        shiftBuffer.Add(new NoteOffEvent(currentNote, beatVelocity), currentCursor, note);
                                        currentCursor += stepSize;
                                    }
                                }
                                else
                                {
                                    currentCursor += stepSize;
                                }

                                var steps = (note.slide == "downwards" || note.slide == "upwards") ? totalSteps + 1 : totalSteps;
                                for (var i = 1; i < steps; i++)
                                {
                                    shiftBuffer.Add(new PitchBendEvent(8192), currentCursor, note);

                                    currentNote = (SevenBitNumber)(note.NoteNumber + i * direction);
                                    nextNote = (SevenBitNumber)(currentNote + direction);

                                    shiftBuffer.Add(new NoteOffEvent(currentNote, beatVelocity), currentCursor, note);
                                    shiftBuffer.Add(new NoteOnEvent(nextNote, (SevenBitNumber)95), currentCursor, note);

                                    currentCursor += stepSize;

                                }
                            }

                            SuspenseValidation = false;
                            if (beat.notes.Length == 1)
                            {
                                var osk = 0;
                                foreach (var bufferEvent in shiftBuffer)
                                {
                                    events.Add(bufferEvent.Event, new Time(bufferEvent.Time), beat.notes[0]);
                                    osk++;
                                }
                            }
                            else
                            {

                                var chunks = shiftBuffer.Chunk(3).ToList();

                                var strumBase = -(123 * beat.notes.Length);
                                var stepStrum = 123 / 2;
                                var strumDecay = 10;

                                for (var stepIndex = 0; stepIndex < semitoneDistance - 1; stepIndex++)
                                {
                                    for (var noteIndex = 0; noteIndex < beat.notes.Length; noteIndex++)
                                    {
                                        var pitchOffset = beat.notes[noteIndex].NoteNumber - beat.notes[0].NoteNumber;

                                        foreach (var stepEvent in chunks[stepIndex])
                                        {
                                            var strumEvent = stepEvent.Event.Clone();
                                            if (strumEvent is NoteEvent ne)
                                            {
                                                ne.NoteNumber += (SevenBitNumber)pitchOffset;
                                            }

                                            var strumTime = stepEvent.Time + strumBase + noteIndex * (stepStrum - (strumDecay - 1) * stepIndex);
                                            events.Add(strumEvent, new Time(strumTime), beat.notes[noteIndex]);
                                        }
                                    }
                                }
                            }
                        }

                        if (note.vibrato)
                        {
                            events.Add(new ControlChangeEvent((SevenBitNumber)1, (SevenBitNumber)64), currentCursor, note);

                            if (note.slide != null)
                                currentCursor += actualDuration / 2;
                            else
                                currentCursor += actualDuration;
                            events.Add(new ControlChangeEvent((SevenBitNumber)1, (SevenBitNumber)0), currentCursor, note);
                        }

                        // Legato
                        if (!note.tie && !string.IsNullOrEmpty(note.slide) && note.slide == "legato")
                        {
                            if (!note.vibrato) // vibrato already took care of the cursor
                            {
                                actualDuration /= 2;
                                currentCursor += actualDuration;
                            }

                            AddLegatoPitchBends(currentCursor, note, events, actualDuration);
                        }

                        currentCursor += 123;
                    }



                    // NoteOff
                    if (beat.notes.Length > 1)
                    {
                        currentCursor = beatCursor + beat.MusicalDuration;
                        foreach (var note in beat.ReversedNotes)
                        {
                            if (note.WillBeTied()) continue;

                            var noteNumber = note.NoteNumber;
                            if (note.tie)
                            {
                                var tieRoot = note.GetTies().Last();
                                noteNumber = tieRoot.NoteNumber;
                            }

                            if (note.slide == "shift")
                            {
                                var targetNote = note.GetSlideTarget();
                                noteNumber = (SevenBitNumber)(targetNote.NoteNumber + 1);
                            }

                            events.Add(new NoteOffEvent((SevenBitNumber)noteNumber, new SevenBitNumber(123)), currentCursor, note);
                        }


                        continue;
                    }

                    foreach (var note in beat.ReversedNotes)
                    {
                        if (note.rest) continue;

                        var rawNoteDuration = beat.MusicalDuration.Clone();
                        if (note.staccato)
                        {
                            rawNoteDuration /= 2;
                        }

                        var actualDuration = prevBeat?.graceNote == "onBeat"
                            ?  rawNoteDuration - new MusicalTimeSpan(prevBeat.numerator, prevBeat.denominator)
                            : rawNoteDuration!;

                        currentCursor += actualDuration;

                        // NoteOff
                        var nextIdenticalNote = nextBeat?.notes.SingleOrDefault(e => (int)e.StringNumber == (int)note.StringNumber && e.fret == note.fret);
                        {
                            if ((nextIdenticalNote == null || !nextIdenticalNote.tie))
                            {

                                var lastNoteOnEvent = GetLastNoteOnEvent(events, (SevenBitNumber)note.NoteNumber);

                                if (events[^1].Event is PitchBendEvent) // legato case
                                {
                                    currentCursor = new (events[^1].Time + 10);
                                }
                                else if (events[^1].Event is NoteOffEvent)
                                {
                                    if (note.tie)
                                    {
                                        currentCursor = new (events[^1].Time);
                                    }
                                    else
                                    {
                                        currentCursor = new (events[^1].Time + 960);
                                    }
                                }

                                else if (events[^1].Event is NoteOnEvent && !note.tie)
                                {
                                    if (note.slide == "shift")
                                    {
                                        var stepSize = note.GetShiftStepSizeTicks();
                                        currentCursor = new (events[^1].Time + stepSize);
                                    }
                                    else
                                    {
                                        currentCursor = new(actualDuration.Tick + events[^1].Time);
                                    }
                                }
                                else
                                {
                                    if (note.tie)
                                    {
                                        // Alternative good solution dont delete mindlessly
                                        // TimedEvent? tieRoot = null;
                                        // for (var i = events.Count - 1; tieRoot == null; i--)
                                        // {
                                        //     var candidate = events[i];
                                        //     if (candidate.Event is NoteOnEvent noe)
                                        //     {
                                        //         var candidateNoteNumber = noe.NoteNumber;
                                        //         if (candidateNoteNumber == noteNumber)
                                        //         {
                                        //             tieRoot = candidate;
                                        //         }
                                        //     }
                                        // }
                                        // 
                                        // var ends = beatCursor.Add(beat.MusicalDuration, TimeSpanMode.TimeLength);
                                        // var tieEndsAt = ends.ToTicks(tempoMap);
                                        // var leftoverTime = events[^1].Time - tieEndsAt;
                                        // currentCursor = (events[^1].Time - leftoverTime).ToTimeSpan(tempoMap);

                                        var ties = note.GetTies().ToList();
                                        var tieRoot = ties.Last();
                                        var tieRootStartedAt = ties.Last().NoteOnEvent.Time;
                                        var tieLength = ties.Sum(e => e.Beat.MusicalDuration.Tick);
                                        var tieEndsAt = tieRootStartedAt + tieLength;

                                        var leftoverTime = events[^1].Time - tieEndsAt;
                                        currentCursor = new (events[^1].Time - leftoverTime);
                                    }


                                }

                                var lastTen = events.Skip(events.Count - 20).Take(20).ToList();

                                if (note.vibrato)
                                {
                                    if (note.slide != null)
                                    {
                                        currentCursor = beatCursor + note.ActualDuration;
                                    }
                                    else
                                    {
                                        currentCursor = beatCursor + note.ActualDuration;
                                    }
                                }

                                events.Add(new NoteOffEvent(((NoteOnEvent)lastNoteOnEvent.Event).NoteNumber, beatVelocity), currentCursor, note);

                                if (note.slide == "downwards" || note.slide == "upwards")
                                {
                                    events.Add(new NoteOffEvent((SevenBitNumber)note.NoteNumber, beatVelocity), currentCursor, note);
                                }

                                if (note.staccato)
                                {
                                    var staccatoSilence = beat.MusicalDuration - rawNoteDuration;
                                    currentCursor += staccatoSilence;
                                }
                            }
                        }
                    }
                }
            }

            var trackChunk = events.ToTrackChunk();
            midiFile.Chunks.Add(trackChunk);
            Debug.WriteLine($"Part {part.Index} finished without error!");
        }
        midiFile.ReplaceTempoMap(Time.Map);
        return midiFile;
    }

    public static void BuildHeader(Part part, IList<TimedEvent> timedEvents)
    {
        var timeZero = new Time();

        for (var i = 0; i < 9; i++)
        {
            // Program Change
            timedEvents.Add(new ProgramChangeEvent((SevenBitNumber)part.instrumentId), timeZero, null, i, part.partId);
        }

        for (var i = 0; i < 9; i++)
        {
            // Mod Wheel Reset
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)1, (SevenBitNumber)0), timeZero, null, i, part.partId);
        }

        for (var i = 0; i < 9; i++)
        {
            // Pitch Bend Reset
            timedEvents.Add(new PitchBendEvent(8192), timeZero, null, i, part.partId);
        }

        for (var i = 0; i < 9; i++)
        {
            // RPN Pitch Range Setup (Your 4 events)
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)101, (SevenBitNumber)0), timeZero, null, i, part.partId);
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)100, (SevenBitNumber)0), timeZero, null, i, part.partId);
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)6, (SevenBitNumber)24), timeZero, null, i, part.partId);
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)38, (SevenBitNumber)0), timeZero, null, i, part.partId);
        }


        if (!string.IsNullOrEmpty(part.name))
        {
            timedEvents.Add(new SequenceTrackNameEvent(part.name), timeZero, null, null, part.partId);
        }

        if (!string.IsNullOrEmpty(part.instrument))
        {
            timedEvents.Add(new InstrumentNameEvent(part.instrument), timeZero,
                null, null, part.partId);
        }
    }

    public static void AddLegatoPitchBends(Time currentCursor, Nóta note, IList<TimedEvent> timedEvents, Time actualDuration)
    {
        timedEvents.Add(new PitchBendEvent(8195), currentCursor, note);

        if (note.vibrato)
        {
            var target = note.GetSlideTarget();
            var inbetweenNote = Math.Sign(target.NoteNumber - note.NoteNumber) + note.NoteNumber;

            timedEvents.Add(new NoteOnEvent((SevenBitNumber)inbetweenNote, (SevenBitNumber)95), currentCursor, note);
            timedEvents.Add(new NoteOffEvent((SevenBitNumber)note.NoteNumber, (SevenBitNumber)95), currentCursor, note);
        }
        else
        {
            for (var l = 0; l < 99; l++)
            {
                if (l == 98)
                {
                    var fillerTime = actualDuration - 11;
                    actualDuration -= fillerTime;
                    currentCursor += fillerTime;

                    timedEvents.Add(new PitchBendEvent(8888), currentCursor, note);

                }
                else
                {
                    var legatoTime = 8;
                    actualDuration -= legatoTime;
                    currentCursor += legatoTime;

                    timedEvents.Add(new PitchBendEvent(8888), currentCursor, note);
                }
            }
        }
    }

    public static TimedEvent GetLastNoteOnEvent(IList<TimedEvent> events, SevenBitNumber noteNumber)
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