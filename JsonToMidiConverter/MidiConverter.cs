using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using Microsoft.VisualBasic;
using Note = Melanchall.DryWetMidi.Interaction.Note;

namespace JsonToMidiConverter;

internal static class MidiConverter
{
    private const int TicksPerQuarterNote = 15360;
    public static readonly List<(long AbsoluteTime, MidiEvent Event)>[] ReferenceData = GetReferenceMidiData();
    public static bool SuspenseValidation = false;

    public static MidiFile Convert(Song song)
    {
        //DebugShit.CheckConsistency();

        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote) };
        var tempoMap = GetTempo(midiFile, song.parts[0]);

        foreach (var part in song.parts)
        {
            var events = new List<TimedEvent>();
            BuildHeader(part, tempoMap, events);

            if (part.partId == 10) continue;

            ITimeSpan currentCursor = new MusicalTimeSpan();

            foreach (var measure in part.measures)
            {
                var measureCursor = new BarBeatFractionTimeSpan(measure.Index);

                events.Add(new MarkerEvent($"MEASURE_{measure.Index}"), measureCursor, new NoteContext(tempoMap, null), null, part.partId);

                var measureChange = part.automations.tempo.SingleOrDefault(e => e.measure == measure.Index);
                if (measureChange != null && measure.Index != 0)
                {
                    events.Add(new SetTempoEvent(Tempo.FromBeatsPerMinute(measureChange.bpm).MicrosecondsPerQuarterNote), measureCursor, new NoteContext(tempoMap, null), null, part.partId);
                }


                foreach (var beat in measure.Beats)
                {
                    var beatVelocity = (SevenBitNumber)(beat.velocity != null ? Speeds[beat.velocity] : 100);
                    var orderedNotes = beat.notes.OrderByDescending(e => e.StringNumber).ToList();
                    var beatCursor = measureCursor.AddTicks(beat.GetMeasureStartDuration(tempoMap), tempoMap);

                    var prevBeat = beat.GetPrevious();
                    var nextBeat = beat.GetNext();

                    currentCursor = beatCursor;

                    foreach (var note in beat.notes.Take(1))
                    {
                        var ctx = new NoteContext(tempoMap, note);
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

                        var noteNumber = GetNoteNumber(part, note);



                        // NoteOnEvent
                        {
                            foreach (var n in beat.notes.Where(e => !e.tie))
                            {
                                var nNumber = GetNoteNumber(n.Part, n);
                                events.Add(new PitchBendEvent(8192), currentCursor, new NoteContext(tempoMap, n));

                                note.NoteOnEvent = events.Add(new NoteOnEvent(nNumber, beatVelocity), currentCursor, new NoteContext(tempoMap, n)).Event;

                                if (beat.notes.Length > 1)
                                {
                                    currentCursor = currentCursor.AddTicks(123, tempoMap);
                                }
                            }
                        }


                        if (note.slide == "shift")
                        {
                            SuspenseValidation = true;
                            var shiftBuffer = new List<TimedEvent>();

                            var targetNote = nextBeat!.notes.First(n => (int)n.StringNumber == (int)note.StringNumber);
                            var targetPitch = GetNoteNumber(part, targetNote);
                            var direction = targetPitch < noteNumber ? -1 : 1;
                            var semitoneDistance = Math.Abs(targetPitch - noteNumber);

                            if (semitoneDistance <= 1)
                            {
                                var shiftOffsetDuration = TimeConverter.ConvertTo<MusicalTimeSpan>(960, tempoMap);
                                actualDuration = (MusicalTimeSpan)actualDuration.Subtract(shiftOffsetDuration, TimeSpanMode.LengthLength);
                                currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);
                                actualDuration = shiftOffsetDuration;
                                AddLegatoPitchBends(currentCursor, ctx, shiftBuffer, tempoMap, actualDuration);
                            }
                            else
                            {
                                var totalSteps = semitoneDistance - 1;
                                var stepSize = GetShiftStepSizeTicks(nextBeat, beat, note, part, actualDuration, tempoMap);

                                var firstNoteDuration = actualDuration.Subtract((totalSteps * stepSize).ToTimeSpan(tempoMap), TimeSpanMode.LengthLength);
                                currentCursor = currentCursor.Add(firstNoteDuration, TimeSpanMode.TimeLength);
                                shiftBuffer.Add(new PitchBendEvent(8192), currentCursor, ctx);

                                var currentNote = noteNumber;
                                var nextNote = (SevenBitNumber)(currentNote + direction);
                                shiftBuffer.Add(new NoteOnEvent(nextNote, (SevenBitNumber)95), currentCursor, ctx);

                                if (note.tie)
                                {
                                    currentCursor = currentCursor.Add(stepSize.ToTimeSpan(tempoMap), TimeSpanMode.TimeLength);
                                    shiftBuffer.Add(new NoteOffEvent(currentNote, beatVelocity), currentCursor, ctx);
                                }
                                else
                                {
                                    shiftBuffer.Add(new NoteOffEvent(currentNote, beatVelocity), currentCursor, ctx);
                                    currentCursor = currentCursor.Add(stepSize.ToTimeSpan(tempoMap), TimeSpanMode.TimeLength);
                                }

                                // Bridge notes
                                for (var i = 1; i < totalSteps; i++)
                                {
                                    shiftBuffer.Add(new PitchBendEvent(8192), currentCursor, ctx);

                                    currentNote = (SevenBitNumber)(noteNumber + i * direction);
                                    nextNote = (SevenBitNumber)(currentNote + direction);

                                    shiftBuffer.Add(new NoteOffEvent(currentNote, beatVelocity), currentCursor, ctx);
                                    shiftBuffer.Add(new NoteOnEvent(nextNote, (SevenBitNumber)95), currentCursor, ctx);

                                    currentCursor = currentCursor.Add(stepSize.ToTimeSpan(tempoMap), TimeSpanMode.TimeLength);

                                }
                            }

                            SuspenseValidation = false;
                            if (beat.notes.Length == 1)
                            {
                                foreach (var bufferEvent in shiftBuffer)
                                {
                                    events.Add(bufferEvent.Event, bufferEvent.Time.ToTimeSpan(tempoMap), new NoteContext(tempoMap, beat.notes[0]));
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
                                            events.Add(strumEvent, strumTime.ToTimeSpan(tempoMap), new NoteContext(tempoMap, beat.notes[noteIndex]));
                                        }
                                    }
                                }
                            }
                        }

                        // Legato
                        if (!note.tie && !string.IsNullOrEmpty(note.slide) && note.slide == "legato")
                        {
                            actualDuration = (MusicalTimeSpan)actualDuration.Divide(2);
                            currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);

                            AddLegatoPitchBends(currentCursor, ctx, events, tempoMap, actualDuration);
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

                            var noteNumber = GetNoteNumber(note.Part, note);
                            if (note.tie)
                            {
                                var tieRoot = note.GetTies().Last();
                                noteNumber = GetNoteNumber(tieRoot.Part, tieRoot);
                            }

                            if (note.slide == "shift")
                            {
                                var targetNote = note.GetSlideTarget();
                                noteNumber = (SevenBitNumber)(targetNote.NoteNumber + 1);
                            }

                            events.Add(new NoteOffEvent(noteNumber, new SevenBitNumber(123)), currentCursor, new NoteContext(tempoMap, note));
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

                        var noteNumber = GetNoteNumber(part, note);
                        var ctx = new NoteContext(tempoMap, note);


                        // NoteOff
                        var nextIdenticalNote = nextBeat?.notes.SingleOrDefault(e => (int)e.StringNumber == (int)note.StringNumber && e.fret == note.fret);
                        {
                            if ((nextIdenticalNote == null || !nextIdenticalNote.tie))
                            {

                                var lastNoteOnEvent = GetLastNoteOnEvent(events, noteNumber);

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

                                else if (events[^1].Event is NoteOnEvent)
                                {
                                    if (note.slide == "shift")
                                    {
                                        var stepSize = GetShiftStepSizeTicks(nextBeat, beat, note, part, actualDuration, tempoMap);
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
                                events.Add(new NoteOffEvent(((NoteOnEvent)lastNoteOnEvent.Event).NoteNumber, beatVelocity), currentCursor, ctx);

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




    public static (TimedEvent Event, NoteContext Ctx) Add(this IList<TimedEvent> events, MidiEvent midiEvent, ITimeSpan time,
        NoteContext ctx, int? channelOverride = null, int? partId = null, int? noteNumberOverride = null)
    {

        var origNoteNumber = ctx.Note?.NoteNumber;
        if (noteNumberOverride != null)
        {
            ctx.Note.NoteNumber = noteNumberOverride.Value;
        }

        if (midiEvent is ChannelEvent channelEvent)
        {
            channelEvent.Channel = (FourBitNumber)(channelOverride ?? GetNoteChannel(ctx.Note.Part, ctx.Note!));
        }

        var tickTime = TimeConverter.ConvertFrom(time, ctx.TempoMap);
        var eventType = midiEvent.GetType();

        if (!SuspenseValidation)
        {
            var pid = partId ?? ctx.Note.Part.Index;

            if (pid < 10)
            {
                var referenceChunk = ReferenceData[pid];
                var referenceEvent = referenceChunk[events.Count];
                var areTheSameType = referenceEvent.Event.GetType() == eventType;
                Debug.Assert(areTheSameType);

                if (!(midiEvent is PitchBendEvent pitch && pitch.PitchValue == 8888))
                {
                    var warning =
                        $"Time mismatch at Index {events.Count} of {eventType.Name}, Expected = {referenceEvent.AbsoluteTime} vs Actual = {tickTime}";
                    var diff = referenceEvent.AbsoluteTime - tickTime;
                    if (Math.Abs(diff) > 8)
                    {
                        Debug.Assert(referenceEvent.AbsoluteTime == tickTime, warning);
                    }
                }

                if (areTheSameType)
                {
                    var props = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props)
                    {
                        var propName = prop.Name;
                        var referenceValue = prop.GetValue(referenceEvent.Event)!;
                        var actualValue = prop.GetValue(midiEvent)!;

                        if (propName != "DeltaTime" && propName != "Velocity" && propName != "PitchValue")
                        {
                            if (!(propName == "PitchValue" && actualValue.ToString() == "8888"))
                            {
                                Debug.Assert(referenceValue.ToString() == actualValue.ToString(), propName);
                            }
                        }
                    }
                }
            }
        }

        var newEvent = new TimedEvent(midiEvent, tickTime);
        events.Add(newEvent);
        if (ctx.Note != null)
        {
            ctx.Note.Events.Add(newEvent);
        }

        if (noteNumberOverride != null)
        {
            ctx.Note.NoteNumber = origNoteNumber.Value;
        }

        return (newEvent, ctx);
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

        // 3. INTELLIGENT FALLBACK (GM Families)
        // If not explicitly listed, calculate channel based on instrument type.
        // GM IDs: 0-127.

        var id = part.instrumentId;

        if (id >= 0 && id <= 7) return (FourBitNumber)0; // Piano -> Ch 1
        if (id >= 24 && id <= 31) return (FourBitNumber)1; // Guitar -> Ch 2
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
            timedEvents.Add(new ProgramChangeEvent((SevenBitNumber)part.instrumentId), timeZero, new NoteContext(tempoMap, null), i, part.partId);
        }

        for (var i = 0; i < 9; i++)
        {
            // Mod Wheel Reset
            timedEvents.Add(
                new ControlChangeEvent((SevenBitNumber)1, (SevenBitNumber)0), timeZero, new NoteContext(tempoMap, null), i, part.partId);
        }

        for (var i = 0; i < 9; i++)
        {
            // Pitch Bend Reset
            timedEvents.Add(new PitchBendEvent(8192), timeZero, new NoteContext(tempoMap, null), i, part.partId);
        }

        for (var i = 0; i < 9; i++)
        {
            // RPN Pitch Range Setup (Your 4 events)
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)101, (SevenBitNumber)0), timeZero, new NoteContext(tempoMap, null), i, part.partId);
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)100, (SevenBitNumber)0), timeZero, new NoteContext(tempoMap, null), i, part.partId);
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)6, (SevenBitNumber)24), timeZero, new NoteContext(tempoMap, null), i, part.partId);
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)38, (SevenBitNumber)0), timeZero, new NoteContext(tempoMap, null), i, part.partId);
        }


        if (!string.IsNullOrEmpty(part.name))
        {
            timedEvents.Add(new SequenceTrackNameEvent(part.name), timeZero, new NoteContext(tempoMap, null), null, part.partId);
        }

        if (!string.IsNullOrEmpty(part.instrument))
        {
            timedEvents.Add(new InstrumentNameEvent(part.instrument), timeZero,
                new NoteContext(tempoMap, null), null, part.partId);
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
    public record NoteContext(TempoMap TempoMap, Nóta? Note);


    public static void AddLegatoPitchBends(ITimeSpan currentCursor, NoteContext ctx, IList<TimedEvent> timedEvents, TempoMap tempoMap, MusicalTimeSpan actualDuration)
    {
        timedEvents.Add(new PitchBendEvent(8195), currentCursor, ctx);

        for (var l = 0; l < 99; l++)
        {
            if (l == 98)
            {
                var fillerTime = actualDuration - TimeConverter.ConvertTo<MusicalTimeSpan>(11, tempoMap);
                actualDuration -= fillerTime;
                currentCursor = currentCursor.Add(fillerTime, TimeSpanMode.TimeLength);

                timedEvents.Add(new PitchBendEvent(8888), currentCursor, ctx);

            }
            else
            {
                var legatoTime = TimeConverter.ConvertTo<MusicalTimeSpan>(8, tempoMap);
                actualDuration -= legatoTime;
                currentCursor = currentCursor.Add(legatoTime, TimeSpanMode.TimeLength);

                timedEvents.Add(new PitchBendEvent(8888), currentCursor, ctx);
            }
        }
    }

    public static SevenBitNumber GetNoteNumber(Part part, Nóta note)
        => GetNoteNumber(part, note.StringNumber, note.fret, note.harmonic);

    public static SevenBitNumber GetNoteNumber(Part part, double stringNumber, int fret, string? harmonic)
    {



        // 1. DRUM HANDLING
        if (part.instrumentId == 1024 || (int)stringNumber == -1)
        {
            return (SevenBitNumber)fret;
        }

        int openStringPitch = part.tuning.Length == 0
            ? (int)stringNumber // Fallback
            : (int)part.tuning[(int)stringNumber];

        //if (part.partId == 3)
        //{
        //    return (SevenBitNumber)(openStringPitch + fret);
        //}

        // 2. BASE PITCH (Open String)
        // We need the open string pitch first


        // 3. HARMONIC HANDLING
        if (harmonic == "natural")
        {
            // The 'fret' or 'harmonicFret' tells us WHICH harmonic, 
            // but the pitch is an offset from the OPEN string.

            switch (fret) // Or note.harmonicFret
            {
                case 12: return (SevenBitNumber)(openStringPitch + 12);
                case 7: return (SevenBitNumber)(openStringPitch + 19);
                case 5: return (SevenBitNumber)(openStringPitch + 24);
                case 4: return (SevenBitNumber)(openStringPitch + 28);
                case 9: return (SevenBitNumber)(openStringPitch + 28); // 9th fret harmonic is same as 4th
                case 3: return (SevenBitNumber)(openStringPitch + 31); // 3rd fret is +2 Octaves + 5th
                                                                       //default: return (SevenBitNumber)(openStringPitch + 12);
            }
        }

        // 4. STANDARD FRETTED NOTE
        return (SevenBitNumber)(openStringPitch + fret);
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

    public static long GetShiftStepSizeTicks(Beat nextBeat, Beat beat, Nóta note, Part part, ITimeSpan actualDuration, TempoMap tempoMap)
    {
        var noteNumber = GetNoteNumber(part, note);

        var targetNote = nextBeat!.notes.First(n => (int)n.StringNumber == (int)note.StringNumber);
        var targetPitch = GetNoteNumber(part, targetNote);
        var direction = targetPitch < noteNumber ? -1 : 1;
        var semitoneDistance = Math.Abs(targetPitch - noteNumber);

        var totalSteps = semitoneDistance - 1;
        var useStandardStep = 960 * totalSteps <= actualDuration.ToTicks(tempoMap) / 2;
        var stepSize = useStandardStep
            ? 960
            : actualDuration.ToTicks(tempoMap) / 2 / totalSteps;

        return stepSize;
    }

}