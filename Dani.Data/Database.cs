using System.Buffers;
using System.IO.Compression;
using System.Text.Json;
using Dani.Data.Json;
using Dani.Data.Models;
using Dani.Data.Models.Parts;
using Dani.Data.Models.Songs;
using Dani.Data.Serialization;

namespace Dani.Data;

public class Database
{
    private readonly string _path;

    public IReadOnlyList<Record> Records { get; }

    public Database(string path)
    {
        _path = path;

        Records = DaniSerializer.Deserialize<Record>(Path.Combine(_path, "records.dani"));

        var noteHeaders = DaniSerializer.DeserializeHeader(Path.Combine(_path, "notes.dani"));
        var beatHeaders = DaniSerializer.DeserializeHeader(Path.Combine(_path, "beats.dani"));

        Note.LoadHeaders(noteHeaders);
        Beat.LoadHeaders(beatHeaders);
    }

    public IReadOnlyList<Part> GetParts(int songId)
    {
        var record = Records.Single(e => e.SongId == songId);
        var partFile = Path.Combine(_path, $"parts_{record.PartFile}.dani");
        return DaniSerializer.DeserializeRange<Part>(partFile, record.PartFileOffset, record.PartCount);
    }

    public Record? GetBest(string filter) =>
        Records.Where(e =>
                e.Artist.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                e.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            .MaxBy(e => e.Views);


    public Record? Get(string artist, string title) =>
        Records.Where(e =>
                e.Artist.Contains(artist, StringComparison.CurrentCultureIgnoreCase) &&
                e.Title.Contains(title, StringComparison.CurrentCultureIgnoreCase))
            .MaxBy(e => e.Views);

    public void Deserialize()
    {
        var files = Directory.GetFiles(_path, "parts_*.dani");
        const int bufferSize = 64 * 1024 * 1024;
        int counter = 0;
        var parts = new List<Part>();

        Parallel.ForEach(files, () => new List<Part>(12500), (file, state, localParts) =>
        {
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: false);

                var reader = new SpanStreamReader(fs, sharedBuffer);
                while (reader.HasData)
                {
                    reader.EnsureBuffer();

                    var part = new Part();
                    int localCursor = 0;
                    part.Read(reader.CurrentSpan, ref localCursor);
                    localParts.Add(part);
                    reader.Advance(localCursor);
                    var c = Interlocked.Increment(ref counter);
                    if (c % 100 == 0) Console.WriteLine($"Desered {c}...");
                }

                return localParts;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }
        }, localParts => parts.AddRange(localParts));


        Console.WriteLine(parts.Count);

    }

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

    private static void SerializeRecords(string outputFolder, IReadOnlyList<Record> records)
    {
        var file = Path.Combine(outputFolder, "records.dani");
        using var outputStream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None, 4096);

        _ = DaniSerializer.Serialize(outputStream, records).ToList();
    }

    private static IReadOnlyList<Record> SerializeMeta(string metaFolder, string outputFolder)
    {
        var files = Directory.GetFiles(metaFolder, "*.json");
        using var outputStream = new FileStream(Path.Combine(outputFolder, "meta.dani"), FileMode.Create, FileAccess.Write, FileShare.None, 4096);
        var metaModels = files.Select<string, MetaData>(e =>
        {
            using var inputStream = new FileStream(e, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize(inputStream, JsonContext.Default.MetaData)!;
        });

        var records = new List<Record>();
        foreach (var meta in DaniSerializer.Serialize(outputStream, metaModels))
        {
            if (meta.Model.SongId <= 0) continue;

            records.Add(new Record
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

    public static List<string> GetRawParts(string root, Record record)
    {
        var parts = new List<string>();

        for (var i = 0; i < record.PartCount; i++)
        {
            var file = Path.Combine(root, $"{record.SongId}_{record.RevisionId}_{i}.gz");
            using var inputStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressionStream);
            var json = reader.ReadToEnd();
            using var document = JsonDocument.Parse(json);
            
            parts.Add(JsonSerializer.Serialize(document, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        return parts;
    }

    private static void SerializeParts(string dataFolder, string outputFolder, IReadOnlyList<Record> records)
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

            foreach (var part in DaniSerializer.Serialize<Part>(outputStream, models))
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