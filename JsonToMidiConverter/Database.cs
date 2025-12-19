using JsonToMidiConverter.Models;
using JsonToMidiConverter.Models.Song;
using Serializer;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZstdSharp;

namespace JsonToMidiConverter;

public class Range
{
    public static readonly ConcurrentBag<Range> Instances = new();

    public double Min = double.MaxValue;
    public double Max = double.MinValue;

    public string Name;
    public int Count;
    public ConcurrentDictionary<double, int> Set = new();
    public bool IsInteger = true;

    private Range()
    {
        Instances.Add(this);
    }

    public Range(string name) : this()
    {
        Name = name;
    }

    public Range(double min, double max, string name, int count, bool isInteger)
     : this(name)
    {
        Min = min;
        Max = max;
        Count = count;
        IsInteger = isInteger;
    }


    public void Update(double value)
    {
        Min = Math.Min(value, Min);
        Max = Math.Max(value, Max);
        Count++;

        Set.AddOrUpdate(value, 1, (key, value) => value + 1);

        if (IsInteger && value != Math.Floor(value))
        {
            IsInteger = false;
        }

    }

    public void Report()
    {
        Console.WriteLine($"Name: {Name}; Min: {Min}; Max: {Max}; Int: {IsInteger}; Count: {Count}, Unique: {Set.Count}");
    }

    public static void ReportAll()
    {
        foreach (var instance in Instances)
        {
            instance.Report();
        }
    }
}
public static class Database
{
    public static readonly string RootPath = @"c:\src\data\";
    public static readonly string SearchPath = Path.Combine(RootPath, "Search");
    public static readonly string MetaPath = Path.Combine(RootPath, "Meta");
    public static readonly string DatabaseFile = Path.Combine(RootPath, "Database.json");
    public static readonly string DataPath = Path.Combine(RootPath, "Data");
    public static readonly string DumpPath = Path.Combine(RootPath, "Dump");
    public static readonly string BinPath = Path.Combine(RootPath, "Bin");
    public static readonly string ZstdPath = Path.Combine(RootPath, "zstd");
    public static readonly string SummaryPath = Path.Combine(RootPath, "Summary");

    public static void SerializeAll()
    {
        PartSerializer serializer = new PartSerializer();
        serializer.Serialize(DataPath, SummaryPath);
    }

    public static readonly HashSet<char> WeirdoCharacters = new[] { '/', '?', '_' }.ToHashSet();

    private static List<RecordModel> Songs;
    private static readonly Dictionary<int, RecordModel> SongsById;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    static Database()
    {
        Songs = LoadDatabase();
        SongsById = Songs.ToDictionary(e => e.SongId);
    }




    public static async Task DeserEndToEnd()
    {
        int counter = 0;

        var bag = new ConcurrentBag<int>();

        //foreach (var metaFile in Directory.GetFiles(MetaPath))
        await Parallel.ForEachAsync(Directory.GetFiles(MetaPath), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (metaFile, _) =>
        {
            var id = int.Parse(Path.GetFileNameWithoutExtension(metaFile));

            try
            {
                await using var metaFileStream = File.OpenRead(metaFile);
                var meta = JsonSerializer.Deserialize<SongMetaDataModel>(metaFileStream, JsonOptions);
                var song = new Song { SongId = id, RevisionId = meta.RevisionId };

                foreach (var partFile in Directory.GetFiles(DataPath, $"{id}_{meta.RevisionId}_*"))
                {
                    await using var originalFileStream = File.OpenRead(partFile);
                    await using var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
                    song.Parts.Add(JsonSerializer.Deserialize<Part>(decompressionStream, JsonOptions)!);
                }

                //var bytes = DaniSerializer.Serialize(song).ToArray();
                Interlocked.Increment(ref counter);
                if (counter % 100 == 0)
                {
                    Console.WriteLine($"Processed {counter} files...");
                }
            }
            catch (Exception e)
            {
                bag.Add(id);
                Console.WriteLine(e);
            }

        });

        Console.WriteLine($"Fucekdup Ids: {string.Join(",", bag)}");
    }

    public static void CompressToFile(byte[] data, string filePath)
    {
        using var compressor = new Compressor(22);
        ReadOnlySpan<byte> source = data;
        var compressedSpan = compressor.Wrap(source);
        using var fs = File.Create(filePath);
        fs.Write(compressedSpan);
    }

    public static async Task ProcessBeats()
    {
        var files = Directory.GetFiles(SummaryPath, "Beats_Partial_*.dani");

        var analyzer = new FlagCorrelationAnalyzer(
            "Tremolo.Tone != 0",
            "Tremolo.Points.Count > 0",
            "Tremolo.Points.AnyNonNull"
        );

        var counter = 0;

        var i = 0;
        var deser = new DaniSerializer();
        await Parallel.ForEachAsync(files, new ParallelOptions(){MaxDegreeOfParallelism = Environment.ProcessorCount }, async (file, ct) =>
        {

            await using var stream = File.OpenRead(Path.Combine(SummaryPath, file));
            //using var reader = new BinaryReader(stream);

            while (stream.Position < stream.Length)
            {
                deser.Deserialize<Beat>(new MemoryStream());

              
                Interlocked.Increment(ref counter);
                if (counter % 100 == 0)
                {
                    Console.WriteLine($"Processed {counter} files...");
                }
            }

        });


        //var numberOfBeats = DaniSerializer.Groups.SelectMany(e => e.Value).Count();
        //var numberOfGroups = DaniSerializer.Groups.Count;
        //var beatsByCount = DaniSerializer.Groups.OrderByDescending(e => e.Value.Count);
        //var avgReuse = (double)numberOfBeats / numberOfGroups;



        Range.ReportAll();


        Console.WriteLine(analyzer.GenerateReport());
    }

    public static void Idk()
    {
        var data = JsonSerializer.Deserialize<List<Bend>>(File.ReadAllText("D:\\randomszar2.json"));
        var tones = string.Join(",", data.Select(e => e.Tone).ToHashSet());

        //var cc = data.Count(e => e.LegacyFlag);
        //Console.WriteLine(cc);
        //var points = data.SelectMany(e => e.Points).ToList();
        //var vibratos = string.Join(",", points.Select(e => e.Vibrato).ToHashSet());
        //var positions = string.Join(",", points.Select(e => e.Position).ToHashSet());
        //var pointtones = string.Join(",", points.Select(e => e.Tone).ToHashSet());

    }

    public static async Task DeserializeRawJsons()
    {
        int counter = 0;
        //var allFiles = Directory
        //    .GetFiles(MetaPath)
        //    .Select(e => int.Parse(Path.GetFileNameWithoutExtension(e)))
        //    .Select(e => new
        //    {
        //        //Id = e,
        //        Parts = Directory.GetFiles(DataPath, $"{e}_*")
        //    })
        //    .ToList();


        var allFiles = Directory.GetFiles(DataPath);

        var chunks = allFiles.Chunk(allFiles.Length / Environment.ProcessorCount).Select((chunk, index) => new
        {
            Chunk = chunk,
            Index = index
        });

        var deser = new DaniSerializer();

        //foreach (var metaFile in Directory.GetFiles(MetaPath))
        await Parallel.ForEachAsync(chunks, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (chunk, _) =>
        {
            //await using var fileStream = File.OpenWrite(Path.Combine(SummaryPath, $"Beats_Partial_{chunk.Index}.dani"));

            //await using var metaFileStream = File.OpenRead(metaFile);
            //var meta = JsonSerializer.Deserialize<SongMetaDataModel>(metaFileStream, JsonOptions);

            foreach (var partFile in chunk.Chunk)
            {
                //foreach (var partFile in item.Parts)
                {
                    try
                    {
                        await using var originalFileStream = File.OpenRead(partFile);
                        await using var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
                        var part = JsonSerializer.Deserialize<Part>(decompressionStream, JsonOptions);

                        var thisBeats = part.Measures
                            .SelectMany(e => e.Voices)
                            .SelectMany(e => e.Beats)
                            .ToList();

                        foreach (var beat in thisBeats)
                        {
                            deser.Serialize(beat);
                        }
                    }
                    catch (Exception ex)
                    {
                        DumpFile(partFile);
                        throw ex;
                    }
                }

                Interlocked.Increment(ref counter);
                if (counter % 100 == 0)
                {
                    Console.WriteLine($"Processed {counter} files...");
                }
            }
        });


        var beats = deser.Groups.Keys;
        using var beatStream = File.OpenWrite(Path.Combine(SummaryPath, "packedbeats.dani"));
        foreach (var beat in beats)
        {
                beatStream.Write(beat, 0, beat.Length);
        }


        //var json = JsonSerializer.Serialize(beats, JsonOptions);
        //await File.WriteAllTextAsync(Path.Combine(SummaryPath, "allbeats.json"), json);

    }

    public static void TestAll()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var c = 0;
        foreach (var file in Directory.GetFiles(DataPath))
        {
            using var originalFileStream = File.OpenRead(file);
            using var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);

            try
            {
                var result = JsonSerializer.Deserialize<Part>(decompressionStream, JsonOptions);
                //Console.WriteLine($"{c++}: {file} Ok... {result.Measures.Count}");

                var shits = result.Measures;

                foreach (var shit in shits)
                {
                    //if (shit.TripletFeel != null && set.Add(shit.TripletFeel))
                    //{
                    //    Console.WriteLine(shit.TripletFeel);
                    //}
                }

            }
            catch (Exception e)
            {
                DumpFile(file);

                Console.WriteLine("Fuckedup");
                if (e.Message.Contains(
                        "The JSON value could not be converted to System.String. Path: $.measures[0].voices[0].beats[0].text.text | LineNumber: 0 | BytePositionInLine: 263.'"))
                {
                    continue;
                }

                throw e;
            }
        }

        Console.WriteLine("All done");
    }

    private static void DumpFile(string file)
    {
        using var originalFileStream = File.OpenRead(file);
        using var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
        using var outputStream = File.Create(Path.Combine(DumpPath, "dump.json"));
        decompressionStream.CopyTo(outputStream);
    }

    public static async Task FullScan()
    {
        var ids = Enumerable.Range(100000, 100000);

        await Parallel.ForEachAsync(ids, async (i, b) =>
        {
            await Task.Delay(300, b);
            await RefreshSong(i);
        });
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


    public static RecordModel Get(int songId) => Songs.Single(e => e.SongId == songId);

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
            Parts = e.Tracks?.Count ?? 0,
            Views = e.Views
        };

    public static async Task<SearchResultsModel?> OnlineSearch(string filter)
    {
        if (string.IsNullOrEmpty(filter)) return null;

        var client = new HttpClient();
        var response = await client.GetAsync(
            $"https://www.songsterr.com/api/search?pattern={filter}&inst=undefined&tuning=undefined&difficulty=undefined&size=50&from=0&more=true");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SearchResultsModel>(content, JsonOptions);
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

    public static List<int> FailedIds { get; set; } = [];

    public static async Task RefreshSong(int songId)
    {
        var client = new HttpClient();

        Console.WriteLine($"Scanning: {songId}");

        var metaResponse = await client.GetAsync($"https://www.songsterr.com/api/meta/{songId}");
        if (!metaResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to load: {songId} with status code: {metaResponse.StatusCode}");
            FailedIds.Add(songId);
            return;
        }

        var metaText = await metaResponse.Content.ReadAsStringAsync();
        var meta = JsonSerializer.Deserialize<SongMetaDataModel>(metaText, JsonOptions);
        Console.WriteLine($"Meta found: {meta.Artist} {meta.Title}");

        await File.WriteAllTextAsync(Path.Combine(MetaPath, $"{songId}.json"), metaText);
        for (var i = 0; i < meta.Tracks.Count; i++)
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