using System.IO.Compression;
using System.Text.Json;
using Beat = Api.Models.Models.Parts.Beat;
using JsonContext = Api.Models.Json.JsonContext;
using Note = Api.Models.Models.Parts.Note;

namespace Serializer;

public static class DbBuilder
{
    public static void Build(string dbFolder)
    {
        var metaFolder = Path.Combine(dbFolder, "meta");
        var outputFolder = Path.Combine(dbFolder, "summary");
        var partsFolder = Path.Combine(dbFolder, "data");

        Console.WriteLine("Building meta data...");
        var records = SerializeMeta(metaFolder, outputFolder);

        Console.WriteLine("Building parts...");
        SerializeParts(partsFolder, outputFolder, records);

        Console.WriteLine("Building index...");
        SerializeRecords(outputFolder, records);
    }

    private static void SerializeRecords(string outputFolder, IReadOnlyList<Api.Models.Models.Record> records)
    {
        var file = Path.Combine(outputFolder, "records.dani");
        using var outputStream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None, 4096);

        _ = DaniSerializer.Serialize(outputStream, records).ToList();
    }

    private static IReadOnlyList<Api.Models.Models.Record> SerializeMeta(string metaFolder, string outputFolder)
    {
        var files = Directory.GetFiles(metaFolder, "*.json");
        using var outputStream = new FileStream(Path.Combine(outputFolder, "meta.dani"), FileMode.Create, FileAccess.Write, FileShare.None, 4096);
        var metaModels = files.Select(e =>
        {
            using var inputStream = new FileStream(e, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize(inputStream, JsonContext.Default.MetaData)!;
        });

        var records = new List<Api.Models.Models.Record>();
        foreach (var meta in DaniSerializer.Serialize(outputStream, metaModels))
        {
            if (meta.Model.SongId <= 0) continue;

            records.Add(new Api.Models.Models.Record
            {
                Artist = meta.Model.Artist,
                Title = meta.Model.Title,
                RevisionId = meta.Model.RevisionId,
                Views = meta.Model.Views,
                PartCount = meta.Model.Tracks.Count,
                SongId = meta.Model.SongId,
            });
        }

        return records;
    }


    private static void SerializeParts(string dataFolder, string outputFolder, IReadOnlyList<Api.Models.Models.Record> records)
    {
        var mdop = Environment.ProcessorCount;

        var chunkSize = (int)Math.Ceiling((double)records.Count / mdop);
        var chunks = records.Chunk(chunkSize).ToList();

        var recordsDict = records.ToDictionary(e => e.SongId);

        Parallel.ForEach(chunks, new ParallelOptions { MaxDegreeOfParallelism = mdop }, (chunk, _, index) =>
        {
            var path = Path.Combine(outputFolder, $"parts_{index}.dani");
            using var outputStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096);

            var parts = chunk
                .SelectMany(e => Enumerable.Range(0, e.PartCount).Select(i => new { PartIndex = i, Record = e }));

            var models = parts.Select(e =>
            {
                var file = Path.Combine(dataFolder, $"{e.Record.SongId}_{e.Record.RevisionId}_{e.PartIndex}.gz");
                using var inputStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress);
                var model = JsonSerializer.Deserialize(decompressionStream, JsonContext.Default.Part)!;
                model.Index = e.PartIndex; // just yet another data inconsistency issue
                return model;
            });

            foreach (var part in DaniSerializer.Serialize(outputStream, models))
            {
                if (part.Model.Index == 0)
                {
                    var song = recordsDict[part.Model.SongId];
                    song.PartFile = (int)index;
                    song.PartFileOffset = (int)part.Cursor;
                }
            }
        });

        DaniSerializer.SerializeHeader(Note.GetHeaders(), Path.Combine(outputFolder, "notes.dani"));
        DaniSerializer.SerializeHeader(Beat.GetHeaders(), Path.Combine(outputFolder, "beats.dani"));
    }
}
