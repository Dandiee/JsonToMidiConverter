using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter.Test;

public static class DebugShit
{
    public static void CollectSlideInformation()
    {
        var songPairs = new Dictionary<string, string>
        {
            [@"References\LinkinPark.mid"] = "In the end",
            //[@"References\Nirvana.mid"] = "Come as you are",
        };

        foreach (var pair in songPairs)
        {
            var match = Database.Search(pair.Value).First();
            var data = Database.GetMidiData(match.SongId);
            var song = JsonSerializer.Deserialize<Song>(data, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var outputMidi = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(15360) };
            Time.Map = song.Parts[0].GetTempo(outputMidi);
            outputMidi.ReplaceTempoMap(Time.Map);
            song.Build();

            var midi = MidiFile.Read(pair.Key);

            var midiParts = midi.Chunks.OfType<TrackChunk>().ToList();

            foreach (var part in song.Parts)
            {
                var midiPart = midiParts[part.Index];
                var midiEvents = midiPart.Events.ToList();

                var cursor = 0;

                foreach (var measure in part.Measures)
                {
                    foreach (var beat in measure.Beats)
                    {
                        if (beat.Rest) continue;

                        var leadNote = beat.Notes.FirstOrDefault(e => !e.Tie);
                        if (leadNote == null) continue;
                        cursor = GetNextAttackNoteEvent(midiEvents, cursor, leadNote);
                    }
                }
            }
        }
    }

    public static int GetNextAttackNoteEvent(List<MidiEvent> events, int cursor, Nóta note)
    {


        while (true)
        {
            // if (cursor >= events.Count - 2) return null;

            cursor++;

            var cursorEvent = events[cursor];
            if (cursorEvent is PitchBendEvent pitch && pitch.PitchValue == 8192)
            {
                var nextEvent = events[cursor + 1];
                if (nextEvent is NoteOnEvent on
                    && on.DeltaTime == 0
                    && on.Channel == note.Channel
                    && on.NoteNumber == note.NoteNumber)
                {

                    var measureCursor = cursor;
                    for (var i = measureCursor; ; i--)
                    {
                        var ev = events[i];
                        if (ev is MarkerEvent marker)
                        {
                            var measureIndex = int.Parse(string.Join("", marker.Text.Where(char.IsDigit)));
                            if (measureIndex != note.Measure.Index)
                            {
                                break;
                            }
                            else break;
                        }
                    }



                    return cursor + 1;
                }
            }
        }
    }

    public static List<List<MidiEvent>> GetMidiMeasures(ICollection<MidiEvent> events)
    {
        var retval = new List<List<MidiEvent>>();
        List<MidiEvent> currentMeasure = null;

        foreach (var midiEvent in events)
        {
            currentMeasure?.Add(midiEvent);

            if (midiEvent is MarkerEvent)
            {
                currentMeasure = new List<MidiEvent>();
                retval.Add(currentMeasure);
            }
        }

        return retval;
    }

    public static void WriteDebugFile(Song song)
    {
        var sb = new StringBuilder();

        foreach (var part in song.Parts.OrderBy(e => e.PartId))
        {
            sb.AppendLine($"Part{part.PartId.ToString().PadLeft(2)}, TempCount: {part.Automations.Tempo.Length}; Bal: {part.Balance}; Vol: {part.Volume}; Frets: {part.Frets}, Strings: {part.Strings}; MesCount: {part.Measures.Length}; Name: {part.Name}");

            for (var i = 0; i < part.Measures.Length; i++)
            {
                var measure = part.Measures[i];
                var voice = measure.Voices.Single();
                if (measure.Signature.Length != 0 && measure.Signature.Length != 2) throw new Exception("Cant be");
                var sign1 = measure.Signature.Length == 2 ? measure.Signature[0] : 0;
                var sign2 = measure.Signature.Length == 2 ? measure.Signature[1] : 0;

                sb.AppendLine($"\tMEASURE_{i.ToString().PadLeft(3)}; BeatsCount: {voice.Beats.Length.ToString().PadLeft(2)}, Signiture: [{sign1}, {sign2}]; Rest: {measure.Rest.ToString().PadLeft(5)}");

                for (var j = 0; j < voice.Beats.Length; j++)
                {
                    var beat = voice.Beats[j];
                    if (beat.Duration.Length != 2) throw new Exception("No");

                    var beatJson = JsonSerializer.Serialize(beat, new JsonSerializerOptions(JsonSerializerDefaults.General)
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
                    });


                    sb.AppendLine($"\t\t\tBEAT_{j}: {beatJson}");
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        var fileName = "Data.txt";

        if (!File.Exists(fileName))
        {
            File.WriteAllText(fileName, sb.ToString());
        }
        else
        {
            var og = File.ReadAllText(fileName);
            if (og != sb.ToString())
            {
                File.WriteAllText(fileName, sb.ToString());
            }
        }
    }

    private static JsonSerializerOptions GetTypeExcludedJsonOptions<T>(params string[] excludedProperties)
    {
        var hash = excludedProperties.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo => typeInfo.Properties
                        .Where(e => hash.Contains(e.Name))
                        .ToList()
                        .ForEach(prop => typeInfo.Properties.Remove(prop))
                }
            }
        };
    }

    public static void CheckConsistency(string midiFile, Song song)
    {


        var mid = MidiFile.Read(midiFile);

        var sb = new StringBuilder();
        var tempoMap = mid.GetTempoMap();
        Debug.Assert(mid.Chunks.OfType<TrackChunk>().Count() == mid.Chunks.Count, "All Chunk is TrackChunk");

        sb.AppendLine($"Chunk count: {mid.Chunks.Count}");
        var chunkind = 0;

        var typeMap = new Dictionary<MidiEventType, string>()
        {
            [MidiEventType.NoteOn] = "On",
            [MidiEventType.NoteOff] = "Off",
            [MidiEventType.PitchBend] = "Pitch",
            [MidiEventType.Marker] = "Marker",
            [MidiEventType.Marker] = "Program",
        };
        var attrExclusion = new[] { "Channel", "DeltaTime", "EventType", "NoteNumber" }.ToHashSet();

        var measureOptions = GetTypeExcludedJsonOptions<Measure>(nameof(Measure.Voices));
        var beatOptions = GetTypeExcludedJsonOptions<Beat>(nameof(Beat.Notes), nameof(Beat.Text));
        //var noteOptions = GetTypeExcludedJsonOptions<Nóta>(nameof(Nóta.));

        foreach (var chunk in mid.Chunks.OfType<TrackChunk>())
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine($"Part{chunkind}; EventCount: {chunk.Events.Count}");

            long currentTime = 0;
            var ind = 0;
            var part = song.Parts[chunkind];

            var measureIndex = 0;
            var beat = 0;

            foreach (var midiEvent in chunk.Events)
            {
                currentTime += midiEvent.DeltaTime;

                var properties = midiEvent
                    .GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(e => !attrExclusion.Contains(e.Name));

                var attributes = string.Join("; ", properties
                    .OrderBy(e => e.Name)
                    .Select(prop => $"{prop.Name}: {prop.GetValue(midiEvent)}"));

                if (midiEvent is MarkerEvent marker)
                {
                    sb.AppendLine();
                    sb.AppendLine("----------------------------------------------------------------------------------");
                    sb.AppendLine();

                    if (measureIndex < part.Measures.Length)
                    {
                        sb.AppendLine($"M{part.Measures[measureIndex].Index} {JsonSerializer.Serialize(part.Measures[measureIndex], measureOptions)}");

                        foreach (var b in part.Measures[measureIndex].Beats)
                        {
                            sb.AppendLine($"\tB{b.Index} {JsonSerializer.Serialize(b, beatOptions)}");
                            foreach (var n in b.Notes)
                            {
                                sb.AppendLine(
                                    $"\t\tN{n.Index} {JsonSerializer.Serialize(n, new JsonSerializerOptions(JsonSerializerDefaults.General)
                                    {
                                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
                                    })}");
                            }
                        }

                        sb.AppendLine();

                    }

                    sb.AppendLine($"[{ind}] Part: {chunkind};  {marker.Text};    At: {currentTime}");

                    measureIndex++;

                    if (marker.Text == "END_OF_VOICE")
                    {
                        measureIndex = 0;
                    }
                }
                else
                {
                    if (!typeMap.TryGetValue(midiEvent.EventType, out var niceName))
                    {
                        niceName = midiEvent.EventType.ToString();
                    }


                    var ch = (midiEvent as ChannelEvent)?.Channel.ToString() ?? string.Empty;
                    var nn = (midiEvent as NoteEvent)?.NoteNumber.ToString() ?? string.Empty;
                    if (midiEvent is NoteOnEvent)
                    {
                        sb.AppendLine();
                    }
                    sb.AppendLine($"\t[{ind}] {niceName.PadRight(10)} Note: {nn.PadLeft(2)}; At: {currentTime}; Ch: {ch}; Delta: {midiEvent.DeltaTime.ToString().PadLeft(6)} {attributes}");
                }

                ind++;
            }

            chunkind++;
        }


        var fileName = "current-nightmare.txt";

        if (!File.Exists(fileName))
        {
            File.WriteAllText(fileName, sb.ToString());
        }
        else
        {
            var og = File.ReadAllText(fileName);
            if (og != sb.ToString())
            {
                File.WriteAllText(fileName, sb.ToString());
            }
        }
    }
}