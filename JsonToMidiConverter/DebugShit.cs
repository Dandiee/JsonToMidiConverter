using Melanchall.DryWetMidi.Core;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Melanchall.DryWetMidi.Interaction;

public static class DebugShit
{
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


                    //sb.AppendLine($"\t\t\tBEAT - Ind: {j.ToString().PadLeft(2)}; NotesCount: {beat.notes.Length}; Rest: {beat.rest.ToString().PadLeft(5)}; Type: {beat.type}; MusicalDuration: [{beat.duration[0]}, {beat.duration[1]}]");
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

    public static void CheckConsistency()
    {
        foreach (var file in Directory.GetFiles(@"d:\Songsterr\ReferenceMidis\", "*.mid"))
        {
            var mid = MidiFile.Read(file);

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

            File.WriteAllText(file + ".txt", sb.ToString());
        }
    }
}