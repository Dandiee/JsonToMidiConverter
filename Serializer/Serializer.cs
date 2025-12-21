using Api.Models;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Serializer;

public class PartSerializer
{
    public void Serialize(string dataFolder, string outputFolder)
    {
        // INTEGRITY 1: Clean Output Directory
        // Prevent mixing files from previous runs (The "Stale File" Trap)
        if (Directory.Exists(outputFolder))
        {
            var oldFiles = Directory.GetFiles(outputFolder, "*.dani");
            foreach (var f in oldFiles) File.Delete(f);
        }
        else
        {
            Directory.CreateDirectory(outputFolder);
        }

        // INTEGRITY 2: Reset Static Caches
        // Ensure we start with empty indices (Beat #0 is truly the first beat of THIS run)
        // (Assumes you implemented the Reset method in the Generator as discussed previously)

        var mdop = Environment.ProcessorCount;
        var files = Directory.GetFiles(dataFolder, "*.gz").ToList();

        // Safety: Handle case where files < processors
        if (files.Count == 0) return;
        int chunkSize = (int)Math.Ceiling((double)files.Count / mdop);
        var chunks = files.Chunk(chunkSize).ToList();

        var counter = 0;
        var sw = Stopwatch.StartNew();

        Parallel.ForEach(chunks, new ParallelOptions { MaxDegreeOfParallelism = mdop }, (chunk, _, index) =>
        {
            const int BufferSize = 1024 * 1024 * 64;
            const int SafetyMargin = 1024 * 1024 * 33;

            // Rent buffer to reduce GC pressure
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            var cursor = 0;

            var path = Path.Combine(outputFolder, $"parts_{index}.dani");

            // Use FileOptions.WriteThrough if you want to ensure data hits the disk, 
            // but it's slower. Standard is fine here.
            using var outputStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096);

            try
            {
                foreach (var file in chunk)
                {
                    using var inputStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress);
                    var part = JsonSerializer.Deserialize(decompressionStream, JsonContext.Default.Part);

                    if (cursor > BufferSize - SafetyMargin)
                    {
                        outputStream.Write(buffer, 0, cursor);
                        cursor = 0;
                    }

                    part!.Write(buffer, ref cursor);

                    var c = Interlocked.Increment(ref counter);
                    if (c % 100 == 0) Console.WriteLine($"Processed {c}...");
                }

                if (cursor > 0)
                {
                    outputStream.Write(buffer, 0, cursor);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        });

        sw.Stop();

        // INTEGRITY 4: Save Headers AFTER all parallel tasks are done
        // This ensures _valueList contains every index referenced in parts_*.dani
        WriteHeaders(Note.GetHeaders(), Path.Combine(outputFolder, "notes.dani"));
        WriteHeaders(Beat.GetHeaders(), Path.Combine(outputFolder, "beats.dani"));

        Console.WriteLine($"Serialization Complete: {(sw.ElapsedMilliseconds / 1000f):N1} sec");
    }

    public void Deserialize(string inputFolder)
    {
        var noteHeaders = ReadHeaders(Path.Combine(inputFolder, "notes.dani"));
        var beatHeaders = ReadHeaders(Path.Combine(inputFolder, "beats.dani"));

        Note.LoadHeaders(noteHeaders);
        Beat.LoadHeaders(beatHeaders);

        var files = Directory.GetFiles(inputFolder, "parts_*.dani");
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

 

    public static void WriteHeaders(List<UInt128> index, string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        var rawSpan = CollectionsMarshal.AsSpan(index);
        var byteSpan = MemoryMarshal.AsBytes(rawSpan);
        fs.Write(byteSpan);
    }

    private static List<UInt128> ReadHeaders(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fs.Length % 16 != 0) throw new InvalidDataException($"Corrupted index file: Length ({fs.Length}) is not a multiple of 16.");

        int count = (int)(fs.Length / 16);
        var list = new List<UInt128>(count);
        CollectionsMarshal.SetCount(list, count);
        Span<UInt128> listSpan = CollectionsMarshal.AsSpan(list);
        Span<byte> byteSpan = MemoryMarshal.AsBytes(listSpan);
        fs.ReadExactly(byteSpan);

        return list;
    }
}