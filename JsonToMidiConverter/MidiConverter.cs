using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Diagnostics;

namespace JsonToMidiConverter;

internal static partial class MidiConverter
{
    public static TempoMap TempoMap;

    private const int TicksPerQuarterNote = 15360;
    public static readonly List<(long AbsoluteTime, MidiEvent Event)>[] ReferenceData = GetReferenceMidiData();
    public static bool SuspenseValidation = false;

    public static MidiFile Convert(Song song)
    {
        //DebugShit.CheckConsistency();

        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote) };
        var tempoMap = GetTempo(midiFile, song.parts[0]);
        TempoMap = tempoMap;
        foreach (var part in song.parts)
        {
            var events = new List<TimedEvent>();
            BuildHeader(part, tempoMap, events);

            if (part.partId == 10) continue;

            ITimeSpan currentCursor = new MusicalTimeSpan();

            foreach (var measure in part.measures)
            {
                var measureCursor = new BarBeatFractionTimeSpan(measure.Index);

                events.Add(new MarkerEvent($"MEASURE_{measure.Index}"), measureCursor, null, null, part.partId);

                var measureChange = part.automations.tempo.SingleOrDefault(e => e.measure == measure.Index);
                if (measureChange != null && measure.Index != 0)
                {
                    events.Add(new SetTempoEvent(Tempo.FromBeatsPerMinute(measureChange.bpm).MicrosecondsPerQuarterNote), measureCursor, null, null, part.partId);
                }


                foreach (var beat in measure.Beats)
                {
                    var beatVelocity = (SevenBitNumber)(beat.velocity != null ? Speeds[beat.velocity] : 100);
                    var beatCursor = measureCursor.AddTicks(beat.GetMeasureStartDuration(tempoMap), tempoMap);

                    var prevBeat = beat.GetPrevious();
                    var nextBeat = beat.GetNext();

                    currentCursor = beatCursor;

                    foreach (var note in beat.notes.Take(1))
                    {
                        var rawNoteDuration = (MusicalTimeSpan)beat.MusicalDuration.Clone();

                        if (note.staccato)
                        {
                            rawNoteDuration /= 2;
                        }

                        var actualDuration = prevBeat?.graceNote == "onBeat"
                            ? (MusicalTimeSpan)rawNoteDuration.Subtract(prevBeat.MusicalDuration, TimeSpanMode.LengthLength)
                            : rawNoteDuration;

                        if (note.rest)
                        {
                            currentCursor = currentCursor.Add(beat.MusicalDuration, TimeSpanMode.TimeLength);
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
                                        currentCursor = currentCursor.AddTicks(123, tempoMap);
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
                                var shiftOffsetDuration = TimeConverter.ConvertTo<MusicalTimeSpan>(960, tempoMap);
                                actualDuration = (MusicalTimeSpan)actualDuration.Subtract(shiftOffsetDuration, TimeSpanMode.LengthLength);
                                currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);
                                actualDuration = shiftOffsetDuration;
                                AddLegatoPitchBends(currentCursor, note, shiftBuffer, tempoMap, actualDuration);
                            }
                            else
                            {
                                if (note.slide == "upwards")
                                {

                                }

                                var totalSteps = semitoneDistance - 1;
                                var stepSize = note.GetShiftStepSizeTicks(tempoMap);

                                var firstNoteDuration = actualDuration.Subtract((totalSteps * stepSize).ToTimeSpan(tempoMap), TimeSpanMode.LengthLength);

                                if (note.tie && (note.slide == "downwards" || note.slide == "upwards" ))
                                {
                                    currentCursor = currentCursor.AddTicks(-stepSize, tempoMap);
                                }

                                currentCursor = currentCursor.Add(firstNoteDuration, TimeSpanMode.TimeLength);

                                var currentNote = (SevenBitNumber)note.NoteNumber;
                                var nextNote = (SevenBitNumber)(currentNote + direction);

                                shiftBuffer.Add(new PitchBendEvent(8192), currentCursor, note);
                                shiftBuffer.Add(new NoteOnEvent(nextNote, (SevenBitNumber)95), currentCursor, note);

                                if (note.slide == "shift")
                                {
                                    if (note.tie)
                                    {
                                        currentCursor = currentCursor.Add(stepSize.ToTimeSpan(tempoMap), TimeSpanMode.TimeLength);
                                        shiftBuffer.Add(new NoteOffEvent(currentNote, beatVelocity), currentCursor, note);
                                    }
                                    else
                                    {
                                        shiftBuffer.Add(new NoteOffEvent(currentNote, beatVelocity), currentCursor, note);
                                        currentCursor = currentCursor.Add(stepSize.ToTimeSpan(tempoMap), TimeSpanMode.TimeLength);
                                    }
                                }
                                else
                                {
                                    currentCursor = currentCursor.Add(stepSize.ToTimeSpan(tempoMap), TimeSpanMode.TimeLength);
                                }

                                var steps = (note.slide == "downwards" || note.slide == "upwards") ? totalSteps + 1 : totalSteps;
                                for (var i = 1; i < steps; i++)
                                {
                                    shiftBuffer.Add(new PitchBendEvent(8192), currentCursor, note);

                                    currentNote = (SevenBitNumber)(note.NoteNumber + i * direction);
                                    nextNote = (SevenBitNumber)(currentNote + direction);

                                    shiftBuffer.Add(new NoteOffEvent(currentNote, beatVelocity), currentCursor, note);
                                    shiftBuffer.Add(new NoteOnEvent(nextNote, (SevenBitNumber)95), currentCursor, note);

                                    currentCursor = currentCursor.Add(stepSize.ToTimeSpan(tempoMap), TimeSpanMode.TimeLength);

                                }
                            }

                            SuspenseValidation = false;
                            if (beat.notes.Length == 1)
                            {
                                var osk = 0;
                                foreach (var bufferEvent in shiftBuffer)
                                {
                                    events.Add(bufferEvent.Event, bufferEvent.Time.ToTimeSpan(tempoMap), beat.notes[0]);
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
                                            events.Add(strumEvent, strumTime.ToTimeSpan(tempoMap), beat.notes[noteIndex]);
                                        }
                                    }
                                }
                            }
                        }

                        if (note.vibrato)
                        {
                            events.Add(new ControlChangeEvent((SevenBitNumber)1, (SevenBitNumber)64), currentCursor, note);

                            if (note.slide != null)
                                currentCursor = currentCursor.Add(actualDuration.Divide(2), TimeSpanMode.TimeLength);
                            else
                                currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);
                            events.Add(new ControlChangeEvent((SevenBitNumber)1, (SevenBitNumber)0), currentCursor, note);
                        }

                        // Legato
                        if (!note.tie && !string.IsNullOrEmpty(note.slide) && note.slide == "legato")
                        {
                            if (!note.vibrato) // vibrato already took care of the cursor
                            {
                                actualDuration = (MusicalTimeSpan)actualDuration.Divide(2);
                                currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);
                            }

                            AddLegatoPitchBends(currentCursor, note, events, tempoMap, actualDuration);
                        }
                        currentCursor = currentCursor.ToTicks(tempoMap).ToTimeSpan(tempoMap).AddTicks(123, tempoMap);
                    }



                    // NoteOff
                    if (beat.notes.Length > 1)
                    {
                        currentCursor = beatCursor.Add(beat.MusicalDuration, TimeSpanMode.TimeLength);
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

                        var rawNoteDuration = (MusicalTimeSpan)beat.MusicalDuration.Clone();
                        if (note.staccato)
                        {
                            rawNoteDuration /= 2;
                        }

                        var actualDuration = prevBeat?.graceNote == "onBeat"
                            ? (MusicalTimeSpan)rawNoteDuration.Subtract(new MusicalTimeSpan(prevBeat.numerator, prevBeat.denominator),
                                TimeSpanMode.LengthLength)
                            : rawNoteDuration;

                        currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);

                        // NoteOff
                        var nextIdenticalNote = nextBeat?.notes.SingleOrDefault(e => (int)e.StringNumber == (int)note.StringNumber && e.fret == note.fret);
                        {
                            if ((nextIdenticalNote == null || !nextIdenticalNote.tie))
                            {

                                var lastNoteOnEvent = GetLastNoteOnEvent(events, (SevenBitNumber)note.NoteNumber);

                                if (events[^1].Event is PitchBendEvent) // legato case
                                {
                                    currentCursor = (events[^1].Time + 10).ToTimeSpan(tempoMap);
                                }
                                else if (events[^1].Event is NoteOffEvent)
                                {
                                    if (note.tie)
                                    {
                                        currentCursor = (events[^1].Time).ToTimeSpan(tempoMap);
                                    }
                                    else
                                    {
                                        currentCursor = (events[^1].Time + 960).ToTimeSpan(tempoMap);
                                    }
                                }

                                else if (events[^1].Event is NoteOnEvent && !note.tie)
                                {
                                    if (note.slide == "shift")
                                    {
                                        var stepSize = note.GetShiftStepSizeTicks(tempoMap);
                                        currentCursor = (events[^1].Time + stepSize).ToTimeSpan(tempoMap);
                                    }
                                    else
                                    {
                                        currentCursor = events[^1].Time.ToTimeSpan(tempoMap) + actualDuration;
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
                                        var tieLength = ties.Sum(e => e.Beat.MusicalDuration.ToTicks(tempoMap));
                                        var tieEndsAt = tieRootStartedAt + tieLength;

                                        var leftoverTime = events[^1].Time - tieEndsAt;
                                        currentCursor = (events[^1].Time - leftoverTime).ToTimeSpan(tempoMap);
                                    }


                                }

                                var bug = currentCursor.ToTicks(tempoMap);
                                var lastTen = events.Skip(events.Count - 20).Take(20).ToList();

                                if (note.vibrato)
                                {
                                    if (note.slide != null)
                                    {
                                        currentCursor = beatCursor.Add(note.ActualDuration.Divide(1), TimeSpanMode.TimeLength);
                                    }
                                    else
                                    {
                                        currentCursor = beatCursor.Add(note.ActualDuration, TimeSpanMode.TimeLength);
                                    }
                                }

                                events.Add(new NoteOffEvent(((NoteOnEvent)lastNoteOnEvent.Event).NoteNumber, beatVelocity), currentCursor, note);

                                if (note.slide == "downwards" || note.slide == "upwards")
                                {
                                    events.Add(new NoteOffEvent((SevenBitNumber)note.NoteNumber, beatVelocity), currentCursor, note);
                                }

                                if (note.staccato)
                                {
                                    var staccatoSilence = (MusicalTimeSpan)beat.MusicalDuration.Subtract(rawNoteDuration, TimeSpanMode.LengthLength);
                                    currentCursor = currentCursor.Add(staccatoSilence, TimeSpanMode.TimeLength);
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
        midiFile.ReplaceTempoMap(tempoMap);
        return midiFile;
    }



    public static FourBitNumber GetNoteChannel(Part part, Nóta note)
    {
        if (part.instrumentId == 71 || part.instrumentId == 68 || part.instrumentId == 27 || part.instrumentId == 30)
        {
            return (FourBitNumber)note.StringNumber;
        }

        if (part.instrumentId == 27) return (FourBitNumber)2;

        // 1. DRUMS (Always Channel 10, index 9)
        if (part.instrumentId == 1024) return (FourBitNumber)9;

        // 2. Try explicit lookup first (for special overrides)
        if (InstrumentChannels.TryGetValue(part.instrumentId, out int assignedChannel))
        {
            return (FourBitNumber)assignedChannel;
        }

        if (part.instrumentId == 0 || part.instrumentId == 48 || part.instrumentId == 34) // piano and sampler
        {
            return (FourBitNumber)(int)note.StringNumber;
        }

        // 3. INTELLIGENT FALLBACK (GM Families)
        // If not explicitly listed, calculate channel based on instrument type.
        // GM IDs: 0-127.

        var id = part.instrumentId;

        if (id >= 0 && id <= 7) return (FourBitNumber)0; // Piano -> Ch 1
        if (id >= 24 && id <= 34) return (FourBitNumber)1; // Guitar -> Ch 2
        if (id >= 32 && id <= 39) return (FourBitNumber)2; // Bass   -> Ch 3
        if (id >= 40 && id <= 55) return (FourBitNumber)3; // Strings/Voices -> Ch 4
        if (id >= 56 && id <= 71) return (FourBitNumber)4; // Brass/Reeds -> Ch 5
        if (id >= 16 && id <= 23) return (FourBitNumber)5; // Organ  -> Ch 6

        // Default for everything else (Synths, FX, World)
        return (FourBitNumber)6;
    }

    public static void BuildHeader(Part part, TempoMap tempoMap, IList<TimedEvent> timedEvents)
    {
        var timeZero = new MusicalTimeSpan();

        for (var i = 0; i < 9; i++)
        {
            // Program Change
            timedEvents.Add(new ProgramChangeEvent((SevenBitNumber)part.instrumentId), timeZero, null, i, part.partId);
        }

        for (var i = 0; i < 9; i++)
        {
            // Mod Wheel Reset
            timedEvents.Add(
                new ControlChangeEvent((SevenBitNumber)1, (SevenBitNumber)0), timeZero, null, i, part.partId);
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

    private static List<(long AbsoluteTime, MidiEvent Event)>[] GetReferenceMidiData()
    {
        var referenceMidi = MidiFile.Read("ReferenceOutput.mid");
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

    public static TempoMap GetTempo(MidiFile midi, Part part)
    {
        var bpmChangeByMeasure = part.automations.tempo.ToDictionary(kvp => kvp.measure, kvp => kvp.bpm);
        int[] lastSignature = [];
        var lastBpm = -1;

        using var tempoMapManager = new TempoMapManager(midi.TimeDivision);

        for (var i = 0; i < part.measures.Length; i++)
        {
            var measure = part.measures[i];
            if (measure.signature.Length == 2)
            {
                lastSignature = measure.signature;
            }

            if (bpmChangeByMeasure.TryGetValue(i, out var newBpm))
            {
                lastBpm = newBpm;
            }

            var time = new BarBeatTicksTimeSpan(i, 0, 0);

            if (lastSignature[0] < 1 || lastSignature[1] < 1 || lastBpm < 1)
            {

            }

            tempoMapManager.SetTimeSignature(time, new TimeSignature(lastSignature[0], lastSignature[1]));
            tempoMapManager.SetTempo(time, Tempo.FromBeatsPerMinute(lastBpm));
        }

        return tempoMapManager.TempoMap;
    }



    // A concise dictionary for specific overrides or non-standard IDs
    private static readonly IReadOnlyDictionary<int, int> InstrumentChannels = new Dictionary<int, int>
    {
        // --- Standard General MIDI Overrides ---

        [71] = 1, // Clarinet (used for vocals) -> Ch 5

        // Vocals (often mapped to arbitrary melody instruments in tabs)
        [68] = 4, // Oboe (used for vocals) -> Ch 5

        [52] = 4, // Choir Aahs -> Ch 5
        [53] = 4, // Voice Oohs -> Ch 5
        [54] = 4, // Synth Voice -> Ch 5

        // --- Guitar Pro / Tab Specifics ---

        // Drums (Double check 1024 isn't the only drum ID used in your source)
        [1024] = 9, // Standard Drums
        [127] = 9, // Gunshot (sometimes used as a snare marker)

        // Special Effects
        [119] = 8, // Reverse Cymbal -> Ch 9
        [122] = 8, // Seashore -> Ch 9
    };

    //private static readonly IReadOnlyDictionary<int, int> InstrumentChannels = new Dictionary<int, int>
    //{
    //    // vocal
    //    [71] = 1,
    //    [68] = 1,


    //    [27] = 2,
    //    [30] = 2,

    //    // drum


    //    // piano
    //    [0] = 3,
    //    [34] = 3,
    //    [29] = 3,


    //    [1024] = 9,

    //    // guitar
    //    [48] = 4,
    //    [34] = 4,
    //    [48] = 4,
    //};

    public static readonly IReadOnlyDictionary<string, int> Speeds = new Dictionary<string, int>
    {
        [""] = 112,
        ["fff"] = 112,
        ["f"] = 001,
        ["mf"] = 002,
        ["mp"] = 003
    };

    public static void AddLegatoPitchBends(ITimeSpan currentCursor, Nóta note, IList<TimedEvent> timedEvents, TempoMap tempoMap, MusicalTimeSpan actualDuration)
    {


        timedEvents.Add(new PitchBendEvent(8195), currentCursor, note);

        if (note.vibrato)
        {
            var length = note.ActualDuration.ToTicks(tempoMap);
            var target = note.GetSlideTarget();
            var inbetweenNote = Math.Sign(target.NoteNumber - note.NoteNumber) + note.NoteNumber;

            timedEvents.Add(new NoteOnEvent((SevenBitNumber)inbetweenNote, (SevenBitNumber)95), currentCursor, note);
            timedEvents.Add(new NoteOffEvent((SevenBitNumber)note.NoteNumber, (SevenBitNumber)95), currentCursor, note);
            //currentCursor = currentCursor.AddTicks(960, tempoMap);
            //timedEvents.Add(new NoteOffEvent((SevenBitNumber)note.NoteNumber, (SevenBitNumber)95), currentCursor, new Nóta(tempoMap, target));

        }
        else
        {
            for (var l = 0; l < 99; l++)
            {
                if (l == 98)
                {
                    var fillerTime = actualDuration - TimeConverter.ConvertTo<MusicalTimeSpan>(11, tempoMap);
                    actualDuration -= fillerTime;
                    currentCursor = currentCursor.Add(fillerTime, TimeSpanMode.TimeLength);

                    timedEvents.Add(new PitchBendEvent(8888), currentCursor, note);

                }
                else
                {
                    var legatoTime = TimeConverter.ConvertTo<MusicalTimeSpan>(8, tempoMap);
                    actualDuration -= legatoTime;
                    currentCursor = currentCursor.Add(legatoTime, TimeSpanMode.TimeLength);

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