using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class MidiConverter
{
    private const int TicksPerQuarterNote = 15360;
    private static readonly int[] StandardGuitarTuning = new[] { 64, 59, 55, 50, 45, 40 };

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


    public static long ToTick(this ITimeSpan timeSpan, TempoMap tempoMap) =>
        TimeConverter.ConvertFrom(timeSpan, tempoMap);

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

    // public static FourBitNumber GetChannel(Part part) => (FourBitNumber)InstrumentChannels[part.instrumentId];

    public static FourBitNumber GetNoteChannel(Part part, Nóta note)
    {
        // 1. DRUMS: Always Channel 9 (10th channel 0-indexed)
        // Check your specific ID for drums (your dictionary had [1024] = 9)
        if (part.instrumentId == 1024)
        {
            return (FourBitNumber)9;
        }

        // 2. GUITARS / STRINGED INSTRUMENTS
        // If the JSON provides a specific string index, that is the Channel.
        // This explains why your file initialized Channels 0-8 with the same ProgramChange!
        if (note.StringNumber.HasValue)
        {
            // Cast the string index directly to the channel.
            // "string": 0 -> Channel 0
            // "string": 1 -> Channel 1
            return (FourBitNumber)((int)note.StringNumber.Value);
        }

        // 3. FALLBACK (Vocals, Piano, etc.)
        // Use your dictionary logic
        if (InstrumentChannels.TryGetValue(part.instrumentId, out int baseChannel))
        {
            return (FourBitNumber)baseChannel;
        }

        // Default if unknown
        return (FourBitNumber)0;
    }

    public static MidiFile Convert(Song song)
    {
        //WriteDebugFile(song);
        //CheckConsistency();

        var json = JsonSerializer.Serialize(song.parts[0].measures[19]);

        var midiFile = new MidiFile()
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote)
        };



        var tempoMap = GetTempo(midiFile, song.parts[0]);



        // 2. Process Parts
        foreach (var part in song.parts)
        {
            // Instead of adding directly to a Chunk, we create a list of TimedEvents.
            // These events have an ABSOLUTE time (e.g., "At 3/4 of the song").
            var timedEvents = new List<TimedEvent>();

            // --- INITIALIZATION (Time 0) ---
            var timeZero = new MusicalTimeSpan();

            for (var i = 0; i < 9; i++)
            {
                // Program Change
                timedEvents.Add(new TimedEvent(
                    new ProgramChangeEvent((SevenBitNumber)part.instrumentId) { Channel = (FourBitNumber)i },
                    timeZero.ToTick(tempoMap)));
            }

            for (var i = 0; i < 9; i++)
            {
                // Mod Wheel Reset
                timedEvents.Add(new TimedEvent(
                    new ControlChangeEvent((SevenBitNumber)1, (SevenBitNumber)0) { Channel = (FourBitNumber)i },
                    timeZero.ToTick(tempoMap)));
            }

            for (var i = 0; i < 9; i++)
            {
                // Pitch Bend Reset
                timedEvents.Add(new TimedEvent(new PitchBendEvent(8192) { Channel = (FourBitNumber)i }, timeZero.ToTick(tempoMap)));
            }

            for (var i = 0; i < 9; i++)
            {
                // RPN Pitch Range Setup (Your 4 events)
                timedEvents.Add(new TimedEvent(new ControlChangeEvent((SevenBitNumber)101, (SevenBitNumber)0) { Channel = (FourBitNumber)i }, timeZero.ToTick(tempoMap)));
                timedEvents.Add(new TimedEvent(new ControlChangeEvent((SevenBitNumber)100, (SevenBitNumber)0) { Channel = (FourBitNumber)i }, timeZero.ToTick(tempoMap)));
                timedEvents.Add(new TimedEvent(new ControlChangeEvent((SevenBitNumber)6, (SevenBitNumber)24) { Channel = (FourBitNumber)i }, timeZero.ToTick(tempoMap)));
                timedEvents.Add(new TimedEvent(new ControlChangeEvent((SevenBitNumber)38, (SevenBitNumber)0) { Channel = (FourBitNumber)i }, timeZero.ToTick(tempoMap)));
            }


            if (!string.IsNullOrEmpty(part.name))
                timedEvents.Add(new TimedEvent(new SequenceTrackNameEvent(part.name), timeZero.ToTick(tempoMap)));

            if (!string.IsNullOrEmpty(part.instrument))
                timedEvents.Add(new TimedEvent(new InstrumentNameEvent(part.instrument), timeZero.ToTick(tempoMap)));


            var speeds = new Dictionary<string, int>
            {
                [""] = 112,
                ["fff"] = 112,
                ["f"] = 001,
                ["mf"] = 002,
                ["mp"] = 003
            };


            // This cursor tracks exactly where we are in the song mathematically.
            ITimeSpan currentCursor = timeZero;

            bool previousWasGraceOnBeat = false;
            ITimeSpan previousGraceDuration = new MusicalTimeSpan();

            ITimeSpan? leftoverFromPrevMeasure = null;
            var tiedChannels = new HashSet<int>();

            for (var measureIndex = 0; measureIndex < part.measures.Length; measureIndex++)
            {
                var measure = part.measures[measureIndex];
                var nextMeasure = measureIndex < part.measures.Length - 2
                    ? part.measures[measureIndex + 1]
                    : null;

                // 1. CALCULATE GAP TO GRID (Should be 0 if cursor is perfect, or >0 if we need to catch up)
                var targetMeasureGridLine = new BarBeatTicksTimeSpan(measureIndex, 0, 0);
                var targetGridTick = TimeConverter.ConvertFrom(targetMeasureGridLine, tempoMap);

                if (measureIndex == 19)
                {
                    // targetGridTick = 1167360
                }
                else if (measureIndex == 20)
                {
                    // targetGridTick = 1228800
                }
                else if (measureIndex == 21)
                {
                    // targetGridTick = 1290240
                }

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
                timedEvents.Add(new TimedEvent(new MarkerEvent($"MEASURE_{measureIndex}"),
                    currentCursor.ToTick(tempoMap)));


                if (measure.rest)
                {
                    var timeSig = tempoMap.GetTimeSignatureAtTime(new BarBeatTicksTimeSpan(measureIndex, 0, 0));
                    var measureLength = new MusicalTimeSpan(timeSig.Numerator, timeSig.Denominator);

                    currentCursor = currentCursor.Add(measureLength, TimeSpanMode.TimeLength);
                    continue;
                }

                var voice = measure.voices.Single();
                Beat? prevBeat = null;
                bool isTiedFromPrevious = false;

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

                    var rawDuration = new MusicalTimeSpan(beat.duration[0], beat.duration[1]);

                    var actualDuration = prevBeat?.graceNote == "onBeat"
                        ? (MusicalTimeSpan)rawDuration.Subtract(new MusicalTimeSpan(prevBeat.numerator, prevBeat.denominator),
                            TimeSpanMode.LengthLength)
                        : rawDuration;


                    foreach (var note in beat.notes)
                    {

                        int channel = GetNoteChannel(part, note);
                        if (!beat.rest)
                        {
                            if (!note.tie && timedEvents[^1].Event is not PitchBendEvent)
                            {
                                if (timedEvents[^1].Event is not MarkerEvent)
                                {
                                    var oneTickTime = TimeConverter.ConvertTo<MusicalTimeSpan>(1, tempoMap);
                                    currentCursor = currentCursor.Add(oneTickTime, TimeSpanMode.TimeLength);
                                }

                                timedEvents.Add(new TimedEvent(
                                    new PitchBendEvent(8192) { Channel = GetNoteChannel(part, note) },
                                    currentCursor.ToTick(tempoMap)));

                            }
                        }

                        if (note.rest)
                        {
                            currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);
                            continue;
                        }

                        var velocity = (SevenBitNumber)(beat.velocity != null ? speeds[beat.velocity] : 100);
                        var noteNumber = part.tuning.Length == 0
                             ? (note.StringNumber == -1 ? 0 : note.StringNumber.Value)
                             : part.tuning[(int)note.StringNumber.Value] + note.fret;


                        // NoteOn
                        if (!note.tie)
                        {
                            timedEvents.Add(new TimedEvent(new NoteOnEvent((SevenBitNumber)noteNumber, velocity) { Channel = GetNoteChannel(part, note) },
                                currentCursor.ToTick(tempoMap)));

                            if (note.slide == "shift")
                            {
                                var shiftOffsetDuration = TimeConverter.ConvertTo<MusicalTimeSpan>(960 + 1, tempoMap);
                                var a = actualDuration.ToTick(tempoMap);
                                actualDuration = (MusicalTimeSpan)actualDuration.Subtract(shiftOffsetDuration, TimeSpanMode.LengthLength);
                                var b = actualDuration.ToTick(tempoMap);
                                var a1 = currentCursor.ToTick(tempoMap);
                                currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);
                                var a2 = currentCursor.ToTick(tempoMap);
                                actualDuration = shiftOffsetDuration;
                                var c = actualDuration.ToTick(tempoMap);

                                timedEvents.Add(new TimedEvent(
                                    new PitchBendEvent(8192) { Channel = GetNoteChannel(part, note) },
                                    currentCursor.ToTick(tempoMap)));

                                timedEvents.Add(new TimedEvent(new NoteOnEvent((SevenBitNumber)(noteNumber + 1), velocity) { Channel = GetNoteChannel(part, note) },
                                    currentCursor.ToTick(tempoMap)));

                                timedEvents.Add(new TimedEvent(new NoteOffEvent((SevenBitNumber)noteNumber, velocity) { Channel = GetNoteChannel(part, note) },
                                    currentCursor.ToTick(tempoMap)));
                            }
                        }

                        // Legato
                        if (!note.tie && !string.IsNullOrEmpty(note.slide) && note.slide == "legato")
                        {
                            actualDuration = (MusicalTimeSpan)actualDuration.Divide(2);
                            currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);

                            timedEvents.Add(new TimedEvent(new PitchBendEvent(8195) { Channel = GetNoteChannel(part, note) }, currentCursor.ToTick(tempoMap)));

                            for (var l = 0; l < 99; l++)
                            {
                                if (l == 98)
                                {
                                    var fillerTime = actualDuration - TimeConverter.ConvertTo<MusicalTimeSpan>(11, tempoMap);
                                    actualDuration -= fillerTime;
                                    currentCursor = currentCursor.Add(fillerTime, TimeSpanMode.TimeLength);

                                    timedEvents.Add(new TimedEvent(new PitchBendEvent(8888) { Channel = GetNoteChannel(part, note) }, currentCursor.ToTick(tempoMap)));

                                }
                                else
                                {
                                    var legatoTime = TimeConverter.ConvertTo<MusicalTimeSpan>(8, tempoMap);
                                    actualDuration -= legatoTime;
                                    currentCursor = currentCursor.Add(legatoTime, TimeSpanMode.TimeLength);

                                    timedEvents.Add(new TimedEvent(new PitchBendEvent(8888) { Channel = GetNoteChannel(part, note) }, currentCursor.ToTick(tempoMap)));
                                }
                            }
                        }

                        var nextIdenticalNote = nextBeat?.notes.SingleOrDefault(e => e.StringNumber == note.StringNumber && e.fret == note.fret);
                        if (nextIdenticalNote == null || !nextIdenticalNote.tie)
                        {
                            // Tie ended (or never existed). Fire NoteOff.
                            currentCursor = currentCursor.Add(actualDuration, TimeSpanMode.TimeLength);
                            currentCursor = currentCursor.Subtract(
                                TimeConverter.ConvertTo<MusicalTimeSpan>(1, tempoMap), TimeSpanMode.LengthLength);

                            var shiftedNoteNumber = note.slide == "shift" ? noteNumber + 1 : noteNumber;

                            timedEvents.Add(new TimedEvent(
                                new NoteOffEvent((SevenBitNumber)shiftedNoteNumber, velocity)
                                    { Channel = GetNoteChannel(part, note) },
                                currentCursor.ToTick(tempoMap)
                            ));
                        }
                        else
                        {

                        }
                    }

                    prevBeat = beat;
                }
            }

            var trackChunk = timedEvents.ToTrackChunk();
            midiFile.Chunks.Add(trackChunk);
        }
        midiFile.ReplaceTempoMap(tempoMap);

        var referenceMidi = MidiFile.Read("ReferenceOutput.mid");
        CompareMidis(midiFile, referenceMidi);

        return midiFile;
    }


    private static void CompareMidis(MidiFile actual, MidiFile expected)
    {
        Debug.Assert(actual.TimeDivision.GetType() == expected.TimeDivision.GetType());
        var actualTimeDivision = actual.TimeDivision as TicksPerQuarterNoteTimeDivision;
        var expectedTimeDivision = expected.TimeDivision as TicksPerQuarterNoteTimeDivision;

        Debug.Assert(actualTimeDivision.TicksPerQuarterNote == expectedTimeDivision.TicksPerQuarterNote);

        // Debug.Assert(actual.OriginalFormat == expected.OriginalFormat);
        Debug.Assert(actual.Chunks.Count == expected.Chunks.Count);



        for (var i = 0; i < actual.Chunks.Count; i++)
        {
            var expectedChunk = expected.Chunks[i] as TrackChunk;
            var actualChunk = actual.Chunks[i] as TrackChunk;


            Debug.Assert(expectedChunk != null && actualChunk != null);
            Debug.Assert(actualChunk.ChunkId == expectedChunk.ChunkId);

            var fuckinTimeSigEventFound = false;
            var offset = 1;

            for (var j = 0; j < expectedChunk.Events.Count; j++)
            {
                var expectedEvent = expectedChunk.Events[j];
                if (expectedEvent is TimeSignatureEvent)
                {
                    fuckinTimeSigEventFound = true;
                    continue;
                }

                var actualEvent = actualChunk.Events[j - (fuckinTimeSigEventFound ? offset : 0)];


                if (!(expectedEvent is PitchBendEvent && (actualEvent.DeltaTime != 8 || expectedEvent.DeltaTime == 10 | expectedEvent.DeltaTime == 9)))
                {
                    Debug.Assert(actualEvent.DeltaTime == expectedEvent.DeltaTime);
                }


                var isFound = false;

                isFound |= CompareEvent<ProgramChangeEvent>(actualEvent, expectedEvent, (act, exp) =>
                {
                    var I = i;
                    var J = j;
                    Debug.Assert(act.ProgramNumber == exp.ProgramNumber);
                });

                isFound |= CompareEvent<ControlChangeEvent>(actualEvent, expectedEvent, (act, exp) =>
                {
                    var I = i;
                    var J = j;
                    Debug.Assert(act.ControlNumber == exp.ControlNumber);
                    Debug.Assert(act.ControlValue == exp.ControlValue);
                });

                isFound |= CompareEvent<PitchBendEvent>(actualEvent, expectedEvent, (act, exp) =>
                {
                    var I = i;
                    var J = j;
                    if (act.PitchValue != 8888)
                    {
                        Debug.Assert(act.PitchValue == exp.PitchValue);
                    }
                });

                isFound |= CompareEvent<SequenceTrackNameEvent>(actualEvent, expectedEvent, (act, exp) =>
                {
                    var I = i;
                    var J = j;
                    Debug.Assert(act.Text == exp.Text);
                });

                isFound |= CompareEvent<InstrumentNameEvent>(actualEvent, expectedEvent, (act, exp) =>
                {
                    var I = i;
                    var J = j;
                    Debug.Assert(act.Text == exp.Text);
                });

                isFound |= CompareEvent<MarkerEvent>(actualEvent, expectedEvent, (act, exp) =>
                {
                    var I = i;
                    var J = j;
                    Debug.Assert(act.Text == exp.Text);
                });

                isFound |= CompareEvent<TimeSignatureEvent>(actualEvent, expectedEvent, (act, exp) =>
                {
                    var I = i;
                    var J = j;
                    Debug.Assert(act.ClocksPerClick == exp.ClocksPerClick);
                    Debug.Assert(act.Denominator == exp.Denominator);
                    Debug.Assert(act.Numerator == exp.Numerator);
                    Debug.Assert(act.ThirtySecondNotesPerBeat == exp.ThirtySecondNotesPerBeat);
                });

                isFound |= CompareEvent<SetTempoEvent>(actualEvent, expectedEvent, (act, exp) =>
                {
                    Debug.Assert(act.MicrosecondsPerQuarterNote == exp.MicrosecondsPerQuarterNote);
                });

                isFound |= CompareEvent<NoteEvent>(actualEvent, expectedEvent, (act, exp) =>
                {
                    var I = i;
                    var J = j;
                    Debug.Assert(act.NoteNumber == exp.NoteNumber);
                    if (false)
                    {
                        // TODO: no fuckin clue
                        Debug.Assert(act.Velocity == exp.Velocity);
                    }
                });

                Debug.Assert(isFound);

                CompareEvent<ChannelEvent>(actualEvent, expectedEvent, (act, exp) =>
                {
                    var I = i;
                    var J = j;
                    Debug.Assert(act.Channel == exp.Channel);
                });
            }
        }



    }

    public static bool CompareEvent<T>(MidiEvent actualEvent, MidiEvent expectedEvent, Action<T, T> asserts)
        where T : MidiEvent
    {
        if (expectedEvent is not T) return false;

        Debug.Assert(actualEvent.EventType == expectedEvent.EventType);

        



        var actual = actualEvent as T;
        var expected = expectedEvent as T;

        Debug.Assert(actualEvent != null && expected != null);

        asserts.Invoke(actual, expected);

        return true;
    }


    private static void WriteDebugFile(Song song)
    {
        var sb = new StringBuilder();
        foreach (var part in song.parts.OrderBy(e => e.partId))
        {
            //sb.AppendLine($"PartId: {part.partId.ToString().PadLeft(2)}, TempCount: {part.automations.tempo.Length}; Bal: {part.balance}; Vol: {part.volume}; Frets: {part.frets}, Strings: {part.strings}; MesCount: {part.measures.Length}; Name: {part.name}");
        }

        //sb.AppendLine();

        //sb.AppendLine("Tempó");
        foreach (var part in song.parts.OrderBy(e => e.partId))
        {
            //sb.AppendLine($"\tPartId: {part.partId.ToString().PadLeft(2)}, TempCount: {part.automations.tempo.Length}; Bal: {part.balance}; Vol: {part.volume}; Frets: {part.frets}, Strings: {part.strings}; MesCount: {part.measures.Length}; Name: {part.name}");
            foreach (var tempo in part.automations.tempo)
            {
                //sb.AppendLine($"\t\tMeasure: {tempo.measure.ToString().PadLeft(3)}; Position: {tempo.position}; BPM: {tempo.bpm.ToString().PadLeft(3)}, Type: {tempo.type}");
            }

            //sb.AppendLine();
        }


        foreach (var part in song.parts.OrderBy(e => e.partId))
        {
            sb.AppendLine($"PartId: {part.partId.ToString().PadLeft(2)}, TempCount: {part.automations.tempo.Length}; Bal: {part.balance}; Vol: {part.volume}; Frets: {part.frets}, Strings: {part.strings}; MesCount: {part.measures.Length}; Name: {part.name}");

            for (var i = 0; i < part.measures.Length; i++)
            {
                var measure = part.measures[i];
                var voice = measure.voices.Single();
                if (measure.signature.Length != 0 && measure.signature.Length != 2) throw new Exception("Cant be");
                var sign1 = measure.signature.Length == 2 ? measure.signature[0] : 0;
                var sign2 = measure.signature.Length == 2 ? measure.signature[1] : 0;

                sb.AppendLine($"\tMEASURE_{i.ToString().PadLeft(3)}; BeatsCount: {voice.beats.Length.ToString().PadLeft(2)}, Signiture: [{sign1}, {sign2}]; Rest: {(measure.rest).ToString().PadLeft(5)}");

                for (var j = 0; j < voice.beats.Length; j++)
                {
                    var beat = voice.beats[j];
                    if (beat.duration.Length != 2) throw new Exception("No");

                    var beatJson = JsonSerializer.Serialize(beat, new JsonSerializerOptions(JsonSerializerDefaults.General)
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
                    });


                    sb.AppendLine($"\t\t\tBEAT_{j}: {beatJson}");


                    //sb.AppendLine($"\t\t\tBEAT - Ind: {j.ToString().PadLeft(2)}; NotesCount: {beat.notes.Length}; Rest: {beat.rest.ToString().PadLeft(5)}; Type: {beat.type}; Duration: [{beat.duration[0]}, {beat.duration[1]}]");
                    //for (var k = 0; k < beat.notes.Length; k++)
                    //{
                    //    var note = beat.notes[k];
                    //    var json = JsonSerializer.Serialize(note, new JsonSerializerOptions(JsonSerializerDefaults.General)
                    //    {
                    //        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
                    //    });
                    //    sb.AppendLine($"\t\t\t\tNOTE - Ind: {k}: {json}");
                    //}
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        File.WriteAllText("Data.txt", sb.ToString());
    }

    private static void CheckConsistency()
    {
        foreach (var file in Directory.GetFiles(@"d:\Songsterr\ReferenceMidis\", "*.mid"))
        {
            var mid = MidiFile.Read(file);

            var sb = new StringBuilder();
            var tempoMap = mid.GetTempoMap();
            Debug.Assert(mid.Chunks.OfType<TrackChunk>().Count() == mid.Chunks.Count, "All Chunk is TrackChunk");

            sb.AppendLine($"Chunk count: {mid.Chunks.Count}");
            foreach (var chunk in mid.Chunks.OfType<TrackChunk>())
            {
                sb.AppendLine($"ChunkId: {chunk.ChunkId}; EventCount: {chunk.Events.Count}");

                long currentTime = 0;
                var ind = 0;

                foreach (var e in chunk.Events)
                {
                    var type = e.GetType();
                    var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    var attributes = string.Join("; ", properties.OrderBy(e => e.Name).Select(prop => $"{prop.Name}: {prop.GetValue(e)}"));
                    if (e is MarkerEvent)
                    {
                        sb.AppendLine($"\t[{ind}] {type.Name} [Time: {currentTime}]- {attributes}");
                    }
                    else
                    {
                        sb.AppendLine($"\t\t[{ind}] {type.Name} [Time: {currentTime}]- {attributes}");
                    }

                        

                    currentTime += e.DeltaTime;

                    ind++;
                }
            }

            File.WriteAllText(file + ".txt", sb.ToString());
        }
    }


}
