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
        var beatOptions = GetTypeExcludedJsonOptions<Beat>(nameof(Beat.Notes));

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
                        sb.AppendLine($"M{part.Measures[measureIndex].Index}: {JsonSerializer.Serialize(part.Measures[measureIndex], measureOptions)}");

                        foreach (var b in part.Measures[measureIndex].Beats)
                        {
                            sb.AppendLine($"\tB{b.Index}: {JsonSerializer.Serialize(b, beatOptions)}");
                            foreach (var n in b.Notes)
                            {
                                sb.AppendLine(
                                    $"\t\tN{n.Index}: {JsonSerializer.Serialize(n, new JsonSerializerOptions(JsonSerializerDefaults.General)
                                    {
                                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault })}");
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


        var fileName = Path.GetFileNameWithoutExtension(midiFile) + ".txt";

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