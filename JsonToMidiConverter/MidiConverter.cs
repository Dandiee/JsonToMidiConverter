using System.Diagnostics;
using System.Reflection;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter;

internal static class MidiConverter
{
    private const int TicksPerQuarterNote = 15360;
    public static readonly List<(long AbsoluteTime, MidiEvent Event)>[] ReferenceData = GetReferenceMidiData();

    public static MidiFile Convert(Song song)
    {
        //DebugShit.CheckConsistency();

        var midiFile = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote) };
        var tempoMap = GetTempo(midiFile, song.parts[0]);

        foreach (var part in song.parts)
        {
            var timedEvents = new List<TimedEvent>();
            BuildHeader(part, tempoMap, timedEvents);

            ITimeSpan currentCursor = new MusicalTimeSpan();

            for (var measureIndex = 0; measureIndex < part.measures.Length; measureIndex++)
            {
                var measure = part.measures[measureIndex];
                var nextMeasure = measureIndex < part.measures.Length - 2
                    ? part.measures[measureIndex + 1]
                    : null;

                var targetMeasureGridLine = new BarBeatTicksTimeSpan(measureIndex, 0, 0);
                var targetGridTick = TimeConverter.ConvertFrom(targetMeasureGridLine, tempoMap);

                var cursorTick = TimeConverter.ConvertFrom(currentCursor, tempoMap);
                var gapToGrid = targetGridTick - cursorTick;

                // 2. ALIGN CURSOR TO GRID
                if (gapToGrid > 0)
                {
                    // This fills the "1 tick" gap left by the previous NoteOff logic
                    currentCursor = currentCursor.Add(
                        TimeConverter.ConvertTo<MusicalTimeSpan>(gapToGrid, tempoMap),
                        TimeSpanMode.TimeLength);
                }

                // 3. PLACE MARKER (Exactly at Grid)
                timedEvents.Add(new MarkerEvent($"MEASURE_{measureIndex}"), currentCursor, new NoteContext(tempoMap, part, null), part.partId);


                if (measure.rest)
                {
                    var timeSig = tempoMap.GetTimeSignatureAtTime(new BarBeatTicksTimeSpan(measureIndex, 0, 0));
                    var measureLength = new MusicalTimeSpan(timeSig.Numerator, timeSig.Denominator);

                    currentCursor = currentCursor.Add(measureLength, TimeSpanMode.TimeLength);
                    continue;
                }

                var voice = measure.voices.Single();
                Beat? prevBeat = null;

                for (var beatIndex = 0; beatIndex < voice.beats.Length; beatIndex++)
                {
                    var beat = voice.beats[beatIndex];
                    Beat? nextBeat = null;

                    if (beatIndex < voice.beats.Length - 1)
                    {
                        nextBeat = voice.beats[beatIndex + 1];
                    }
                    else if (nextMeasure != null && nextMeasure.voices.Single().beats.Length > 0)
                    {
                        nextBeat = nextMeasure.voices.Single().beats[0];
                    }

                    

                    foreach (var note in beat.notes)
                    {
                        var ctx = new NoteContext(tempoMap, part, note);

                        var fullBeatDuration = new MusicalTimeSpan(beat.duration[0], beat.duration[1]);
                        var rawDuration = (MusicalTimeSpan)fullBeatDuration.Clone();
                        if (note.staccato)
                        {
                            rawDuration/= 2;
                        }

                        var actualDuration = prevBeat?.graceNote == "onBeat"
                            ? (MusicalTimeSpan)rawDuration.Subtract(new MusicalTimeSpan(prevBeat.numerator, prevBeat.denominator),
                                TimeSpanMode.LengthLength)
                            : rawDuration;

                        


                        if (!beat.rest)
                        {
                            if (!note.tie && timedEvents[^1].Event is not PitchBendEvent)
                            {
                                if (timedEvents[^1].Event is not MarkerEvent)
                                {
                                    var oneTickTime = TimeConverter.ConvertTo<MusicalTimeSpan>(1, tempoMap);
                                    currentCursor = currentCursor.Add(oneTickTime, TimeSpanMode.TimeLength);
                                }

                                timedEvents.Add(new PitchBendEvent(8192), currentCursor, ctx);
                            }
                        }

                        if (note.rest)
                        {
                            currentCursor = currentCursor.Add(fullBeatDuration, TimeSpanMode.TimeLength);
                            continue;
                        }

                        var velocity = (SevenBitNumber)(beat.velocity != null ? Speeds[beat.velocity] : 100);
                        var noteNumber = GetNoteNumber(part, note);

                        SevenBitNumber? bridgeNoteNumberForSliding = null;

                        // NoteOn
                        if (!note.tie)
                        {
                            timedEvents.Add(new NoteOnEvent(noteNumber, velocity), currentCursor, ctx);

                            if (note.slide == "shift")
                            {
                                var shiftOffsetDuration = TimeConverter.ConvertTo<MusicalTimeSpan>(960, tempoMap);
                                actualDuration = (MusicalTimeSpan)actualDuration.Subtract(shiftOffsetDuration, TimeSpanMode.LengthLength);
                                currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);
                                actualDuration = shiftOffsetDuration;

                                timedEvents.Add(new PitchBendEvent(8192), currentCursor, ctx);
                                var targetNoteObj = nextBeat?.notes.FirstOrDefault(n => n.StringNumber == note.StringNumber);
                                int direction = 1; // Default to UP

                                if (targetNoteObj != null)
                                {
                                    var targetPitch = part.tuning.Length == 0
                                        ? targetNoteObj.StringNumber
                                        : part.tuning[(int)targetNoteObj.StringNumber] + targetNoteObj.fret;

                                    // If target is lower than current, direction is -1. Otherwise 1.
                                    if (targetPitch < noteNumber)
                                    {
                                        direction = -1;
                                    }
                                }

                                // 3. CALCULATE BRIDGE NOTE (Current + Direction)
                                bridgeNoteNumberForSliding = (SevenBitNumber)(noteNumber + direction);

                                // 4. INSERT EVENTS
                                // Use a lower velocity (95) for the bridge note to match the reference
                                var bridgeVelocity = (SevenBitNumber)95;

                                timedEvents.Add(new NoteOnEvent(bridgeNoteNumberForSliding.Value, bridgeVelocity), currentCursor, ctx);
                                timedEvents.Add(new NoteOffEvent(noteNumber, velocity), currentCursor, ctx);
                            }
                        }

                        // Legato
                        if (!note.tie && !string.IsNullOrEmpty(note.slide) && note.slide == "legato")
                        {
                            actualDuration = (MusicalTimeSpan)actualDuration.Divide(2);
                            currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);

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

                        var nextIdenticalNote = nextBeat?.notes.SingleOrDefault(e => (int)e.StringNumber == (int)note.StringNumber && e.fret == note.fret);
                        if (nextIdenticalNote == null || !nextIdenticalNote.tie)
                        {
                            // Tie ended (or never existed). Fire NoteOff.
                            currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);
                            currentCursor = currentCursor.Subtract(TimeConverter.ConvertTo<MusicalTimeSpan>(1, tempoMap), TimeSpanMode.LengthLength);

                            var shiftedNoteNumber = bridgeNoteNumberForSliding ?? noteNumber;

                            timedEvents.Add(new NoteOffEvent(shiftedNoteNumber, velocity), currentCursor, ctx);

                            if (note.staccato)
                            {
                                var staccatoSilence = (MusicalTimeSpan)fullBeatDuration.Subtract(rawDuration, TimeSpanMode.LengthLength);
                                currentCursor = currentCursor.Add(staccatoSilence, TimeSpanMode.TimeLength);
                            }
                        }
                    }

                    prevBeat = beat;
                }
            }

            var trackChunk = timedEvents.ToTrackChunk();
            midiFile.Chunks.Add(trackChunk);
        }
        midiFile.ReplaceTempoMap(tempoMap);
        return midiFile;
    }

    public static SevenBitNumber GetNoteNumber(Part part, Nóta note)
        => part.tuning.Length == 0
            ? (SevenBitNumber)((int)note.StringNumber)
            : (SevenBitNumber)((int)part.tuning[(int)note.StringNumber] + note.fret);

    public static void Add(this IList<TimedEvent> events, MidiEvent midiEvent, ITimeSpan time, NoteContext ctx, int? channelOverride = null)
    {
        if (midiEvent is ChannelEvent channelEvent)
        {
            channelEvent.Channel = (FourBitNumber)(channelOverride ?? GetNoteChannel(ctx.Part, ctx.Note!));
        }

        var tickTime = TimeConverter.ConvertFrom(time, ctx.TempoMap);
        var eventType = midiEvent.GetType();

        if (ctx.Part.partId < 10)
        {
            var referenceChunk = ReferenceData[ctx.Part.partId];
            var referenceEvent = referenceChunk[events.Count];

            if (!(midiEvent is PitchBendEvent pitch && pitch.PitchValue == 8888))
            {
                var index = events.Count + 2;
                var expected = referenceEvent.Event;
                var actual = midiEvent;
                var _events = events;

                var warning = $"Time mismatch at Index {events.Count} of {eventType.Name}, Expected = {referenceEvent.AbsoluteTime} vs Actual = {tickTime}";
                var diff = referenceEvent.AbsoluteTime - tickTime;
                if (diff != -1) Debug.Assert(referenceEvent.AbsoluteTime == tickTime, warning);
            }

            var areTheSameType = referenceEvent.Event.GetType() == eventType;

            Debug.Assert(areTheSameType);

            if (areTheSameType)
            {
                var props = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in props)
                {
                    var propName = prop.Name;
                    var referenceValue = prop.GetValue(referenceEvent.Event)!;
                    var actualValue = prop.GetValue(midiEvent)!;

                    if (propName != "DeltaTime" && propName != "Velocity")
                    {
                        if (!(propName == "PitchValue" && actualValue.ToString() == "8888"))
                        {
                            Debug.Assert(referenceValue.ToString() == actualValue.ToString(), propName);
                        }

                    }
                }
            }
        }

        events.Add(new TimedEvent(midiEvent, tickTime));
    }

    public static void BuildHeader(Part part, TempoMap tempoMap, IList<TimedEvent> timedEvents)
    {
        var timeZero = new MusicalTimeSpan();

        for (var i = 0; i < 9; i++)
        {
            // Program Change
            timedEvents.Add(new ProgramChangeEvent((SevenBitNumber)part.instrumentId), timeZero, new NoteContext(tempoMap, part, null), i);
        }

        for (var i = 0; i < 9; i++)
        {
            // Mod Wheel Reset
            timedEvents.Add(
                new ControlChangeEvent((SevenBitNumber)1, (SevenBitNumber)0), timeZero, new NoteContext(tempoMap, part, null), i);
        }

        for (var i = 0; i < 9; i++)
        {
            // Pitch Bend Reset
            timedEvents.Add(new PitchBendEvent(8192), timeZero, new NoteContext(tempoMap, part, null), i);
        }

        for (var i = 0; i < 9; i++)
        {
            // RPN Pitch Range Setup (Your 4 events)
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)101, (SevenBitNumber)0), timeZero, new NoteContext(tempoMap, part, null), i);
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)100, (SevenBitNumber)0), timeZero, new NoteContext(tempoMap, part, null), i);
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)6, (SevenBitNumber)24), timeZero, new NoteContext(tempoMap, part, null), i);
            timedEvents.Add(new ControlChangeEvent((SevenBitNumber)38, (SevenBitNumber)0), timeZero, new NoteContext(tempoMap, part, null), i);
        }


        if (!string.IsNullOrEmpty(part.name))
        {
            timedEvents.Add(new SequenceTrackNameEvent(part.name), timeZero, new NoteContext(tempoMap, part, null),
                part.partId);
        }

        if (!string.IsNullOrEmpty(part.instrument))
        {
            timedEvents.Add(new InstrumentNameEvent(part.instrument), timeZero,
                new NoteContext(tempoMap, part, null), part.partId);
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

    public static FourBitNumber GetNoteChannel(Part part, Nóta note)
    {
        if (part.instrumentId == 1024) return (FourBitNumber)9;
        return (FourBitNumber)((int)note.StringNumber);
        
        // TODO: yeah the string number wont hold up for long i guess
        if (InstrumentChannels.TryGetValue(part.instrumentId, out int baseChannel)) return (FourBitNumber)baseChannel;
        return (FourBitNumber)0;
    }

    private static readonly IReadOnlyDictionary<int, int> InstrumentChannels = new Dictionary<int, int>
    {
        // vocal
        [71] = 1,
        [68] = 1,


        [27] = 2,
        [30] = 2,

        // drum


        // piano
        [0] = 3,
        [34] = 3,
        [29] = 3,


        [1024] = 9,

        // guitar
        [48] = 4,
        [34] = 4,
        [48] = 4,
    };

    public static readonly IReadOnlyDictionary<string, int> Speeds = new Dictionary<string, int>
    {
        [""] = 112,
        ["fff"] = 112,
        ["f"] = 001,
        ["mf"] = 002,
        ["mp"] = 003
    };
    public record NoteContext(TempoMap TempoMap, Part Part, Nóta? Note);

}