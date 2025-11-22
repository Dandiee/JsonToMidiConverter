using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            sb.AppendLine($"PartId: {part.PartId.ToString().PadLeft(2)}, TempCount: {part.Automations.Tempo.Length}; Bal: {part.Balance}; Vol: {part.Volume}; Frets: {part.Frets}, Strings: {part.Strings}; MesCount: {part.Measures.Length}; Name: {part.Name}");

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

    public static void CheckConsistency(string midiFile)
    {


        var mid = MidiFile.Read(midiFile);

        var sb = new StringBuilder();
        var tempoMap = mid.GetTempoMap();
        Debug.Assert(mid.Chunks.OfType<TrackChunk>().Count() == mid.Chunks.Count, "All Chunk is TrackChunk");

        sb.AppendLine($"Chunk count: {mid.Chunks.Count}");
        var chunkind = 0;
        foreach (var chunk in mid.Chunks.OfType<TrackChunk>())
        {
            sb.AppendLine($"ChunkInd: {chunkind++}; EventCount: {chunk.Events.Count}");

            long currentTime = 0;
            var ind = 0;

            foreach (var e in chunk.Events)
            {
                currentTime += e.DeltaTime;
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





                ind++;
            }
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