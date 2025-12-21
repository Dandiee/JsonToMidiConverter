using Api.Models;
using System.Buffers;
using Api.Models.Parts;
using Beat = Api.Models.Parts.Beat;
using Note = Api.Models.Parts.Note;

namespace Serializer;

public class Db(string path)
{
    public IReadOnlyList<Record> Records { get; private set; }

    public void Load()
    {
        Records = DaniSerializer.Deserialize<Record>(Path.Combine(path, "records.dani"));

        var noteHeaders = DaniSerializer.DeserializeHeader(Path.Combine(path, "notes.dani"));
        var beatHeaders = DaniSerializer.DeserializeHeader(Path.Combine(path, "beats.dani"));

        Note.LoadHeaders(noteHeaders);
        Beat.LoadHeaders(beatHeaders);
    }

    public IReadOnlyList<Part> GetParts(int songId)
    {
        var record = Records.Single(e => e.SongId == songId);
        var partFile = Path.Combine(path, $"parts_{record.PartFile}.dani");
        return DaniSerializer.DeserializeRange<Part>(partFile, record.PartFileOffset, record.PartCount);
    }

    public Record? GetBest(string filter) =>
        Records.Where(e =>
                (e.Artist?.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (e.Title?.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .MaxBy(e => e.Views);


    public void Deserialize()
    {
        

        var files = Directory.GetFiles(path, "parts_*.dani");
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
}