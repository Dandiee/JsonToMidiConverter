using JsonToMidiConverter.Models;
using System.IO.Compression;
using System.Text.Json;
using JsonToMidiConverter.Models.Song;

namespace JsonToMidiConverter;

public static class Database
{
    public static readonly string RootPath = @"d:\MidiDatabase\";
    public static readonly string SearchPath = Path.Combine(RootPath, "Search");
    public static readonly string MetaPath = Path.Combine(RootPath, "Meta");
    public static readonly string DatabaseFile = Path.Combine(RootPath, "Database.json");
    public static readonly string Data = Path.Combine(RootPath, "Data");

    private static readonly IReadOnlyList<RecordModel> Songs;
    private static readonly IReadOnlyDictionary<int, RecordModel> SongsById;

    static Database()
    {
        Songs = LoadDatabase();
        SongsById = Songs.ToDictionary(e => e.SongId);
    }

    public static Song GetMidiData(int songId)
    {
        var record = SongsById[songId];

        var files = Enumerable
            .Range(0, record.Parts)
            .Select(i => Path.Combine(Data, $"{record.SongId}_{record.RevisionId}_{i}.gz"))
            .ToList();

        var streams = files
            .Select(DecompressGzip)
            .ToList();

        var textContents = streams.Select(stream =>
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }).ToList();

        streams.ForEach(s => s.Dispose());

        var content = $"{{\"parts\":[{string.Join(", ", textContents)}]}}";

        return JsonSerializer.Deserialize<Song>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static Stream DecompressGzip(string compressedFilePath)
    {
        var fileToDecompress = new FileInfo(compressedFilePath);
        using var originalFileStream = fileToDecompress.OpenRead();
        var decompressedStream = new MemoryStream();
        using var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
        decompressionStream.CopyTo(decompressedStream);
        decompressedStream.Position = 0;
        return decompressedStream;
    }

    public static IReadOnlyList<RecordModel> Search(string filter)
        => Songs.Where(e =>
                (e.Title != null && e.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (e.Artist != null && e.Artist.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.Views)
            .ToList();

    public static SongMetaDataModel GetMetaData(int songId)
        => JsonSerializer.Deserialize<SongMetaDataModel>(File.ReadAllText(Path.Combine(MetaPath, $"{songId}.json")));

    private static List<RecordModel> LoadDatabase()
    {
        if (!File.Exists(DatabaseFile))
        {
            Console.WriteLine("Database is missing, creating new database...");

            CreateDatabase();

            Console.WriteLine("Database created.");
        }

        return JsonSerializer.Deserialize<List<RecordModel>>(File.ReadAllText(DatabaseFile));
    }

    public static void CreateDatabase()
    {
        var metaFiles = Directory.GetFiles(MetaPath, "*.json", SearchOption.TopDirectoryOnly);
        var metaModels = new List<SongMetaDataModel>();
        foreach (var chunk in metaFiles.Chunk(100))
        {
            Console.WriteLine($"Processing chunk of {chunk.Length} files...");

            var models = chunk
                .Select(path => JsonSerializer.Deserialize<SongMetaDataModel>(File.ReadAllText(path)))
                .ToList();

            metaModels.AddRange(models);
        }

        var records = metaModels.DistinctBy(e => e.SongId).Select(e => new RecordModel
        {
            SongId = e.SongId,
            Artist = e.Artist,
            Title = e.Title,
            ArtistId = e.ArtistId,
            RevisionId = e.RevisionId,
            Parts = e.Tracks?.Length ?? 0,
            Views = e.Views
        });

        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(DatabaseFile, json);
    }

    private static void ProcessSearchResults()
    {
        var searchResultFiles = Directory.GetFiles(SearchPath, "*.json", SearchOption.TopDirectoryOnly);

        var searchResultModels = searchResultFiles
            .Select(path => JsonSerializer.Deserialize<SearchResultsModel>(File.ReadAllText(path)))
            .SelectMany(model => model.Records)
            .ToList();

        var distinctSongs = searchResultModels.DistinctBy(e => e.SongId).Select(e => e.SongId);
    }
}