using System.Diagnostics.CodeAnalysis;
using JsonToMidiConverter.Models;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Models.Song;

namespace JsonToMidiConverter;

public static class Database
{
    public static readonly string RootPath = @"d:\MidiDatabase\";
    public static readonly string SearchPath = Path.Combine(RootPath, "Search");
    public static readonly string MetaPath = Path.Combine(RootPath, "Meta");
    public static readonly string DatabaseFile = Path.Combine(RootPath, "Database.json");
    public static readonly string DataPath = Path.Combine(RootPath, "Data");

    public static readonly HashSet<char> WeirdoCharacters = new[] { '/', '?', '_' }.ToHashSet();

    private static List<RecordModel> Songs;
    private static readonly Dictionary<int, RecordModel> SongsById;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

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
            .Select(i => Path.Combine(DataPath, $"{record.SongId}_{record.RevisionId}_{i}.gz"))
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

        var content = $"{{\"parts\":[{string.Join(", ", textContents)}], \"songId\": {record.SongId}}}";

        return JsonSerializer.Deserialize<Song>(content, JsonOptions);
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

    public static string CleanString(string text)
    {
        var result = text;
        foreach (var weirdoCharacter in WeirdoCharacters)
        {
            result = result.Replace(weirdoCharacter.ToString(), "");
        }

        return result;
    }

    public static IReadOnlyList<RecordModel> Search(string artist, string title)
        => Songs.Where(e =>
                (e.Title != null && CleanString(e.Title) == CleanString(title)) ||
                (e.Artist != null && CleanString(e.Artist) == CleanString(artist)))
            .OrderByDescending(e => e.Views)
            .ToList();

    public static IReadOnlyList<RecordModel> Search(string filter)
        => Songs.Where(e =>
                (e.Title != null && e.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                (e.Artist != null && e.Artist.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.Views)
            .ToList();

    public static IEnumerable<RecordModel> GetTop50() => Songs.OrderByDescending(e => e.Views).Take(50).ToList();

    public static SongMetaDataModel GetMetaData(int songId)
    {
        var path = Path.Combine(MetaPath, $"{songId}.json");

        if (!File.Exists(path))
        {
            throw new Exception("Song not found");
        }

        var json = File.ReadAllText(path);
        var model = JsonSerializer.Deserialize<SongMetaDataModel>(json, JsonOptions);

        return model;
    }

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

        var records = metaModels.DistinctBy(e => e.SongId).Select(CreateRecord).ToList();
        SaveDatabase(records);
    }

    private static void SaveDatabase(IEnumerable<RecordModel> records)
    {
        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(DatabaseFile, json);
    }

    private static RecordModel CreateRecord(SongMetaDataModel e) =>
        new()
        {
            SongId = e.SongId,
            Artist = e.Artist,
            Title = e.Title,
            ArtistId = e.ArtistId,
            RevisionId = e.RevisionId,
            Parts = e.Tracks?.Length ?? 0,
            Views = e.Views
        };

    private static void ProcessSearchResults()
    {
        var searchResultFiles = Directory.GetFiles(SearchPath, "*.json", SearchOption.TopDirectoryOnly);

        var searchResultModels = searchResultFiles
            .Select(path => JsonSerializer.Deserialize<SearchResultsModel>(File.ReadAllText(path)))
            .SelectMany(model => model.Records)
            .ToList();

        var distinctSongs = searchResultModels.DistinctBy(e => e.SongId).Select(e => e.SongId);
    }

    public static async Task RefreshSong(int songId)
    {
        var client = new HttpClient();

        var metaResponse = await client.GetAsync($"https://www.songsterr.com/api/meta/{songId}");
        var metaText = await metaResponse.Content.ReadAsStringAsync();

        var meta = JsonSerializer.Deserialize<SongMetaDataModel>(metaText, JsonOptions);

        await File.WriteAllTextAsync(Path.Combine(MetaPath, $"{songId}.json"), metaText);

        for (var i = 0; i < meta!.Tracks.Length; i++)
        {
            var dataUrl = $"https://dqsljvtekg760.cloudfront.net/{meta.SongId}/{meta.RevisionId}/{meta.Image}/{i}.json";
            var dataResponse = await client.GetAsync(dataUrl);
            var dataBytes = await dataResponse.Content.ReadAsByteArrayAsync();

            await File.WriteAllBytesAsync(Path.Combine(DataPath, $"{meta.SongId}_{meta.RevisionId}_{i}.gz"), dataBytes);
        }


        SongsById[songId] = CreateRecord(meta);
        Songs = SongsById.Values.ToList();

        SaveDatabase(Songs);
    }

    public static async Task RefreshTopsSong()
    {
        var topSongs = Songs.OrderByDescending(e => e.Views).Take(50).ToList();
        foreach (var topSong in topSongs)
        {
            await RefreshSong(topSong.SongId);
        }

    }
}