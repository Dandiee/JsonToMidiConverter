using Api.Models;
using System.Buffers;
using System.Text.Json;
using Api.Models.Songs;

namespace Serializer;

public static class DbBuilder
{
    public static void Serialize(string metaFolder, string outputFolder)
    {
        var files = Directory.GetFiles(metaFolder, "*.json");
        using var outputStream = new FileStream(Path.Combine(outputFolder, "meta.dani"), FileMode.Create, FileAccess.Write, FileShare.None, 4096);
        const int bufferSize = 1024 * 1024 * 64;
        const int safetyMargin = 1024 * 1024 * 33;

        var cursor = 0;
        var counter = 0;

        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            foreach (var file in files)
            {
                using var inputStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                var metData = JsonSerializer.Deserialize(inputStream, JsonContext.Default.MetaData);

                if (cursor > bufferSize - safetyMargin)
                {
                    outputStream.Write(buffer, 0, cursor);
                    cursor = 0;
                }

                metData!.Write(buffer, ref cursor);

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
    }

    public static List<MetaData> Deserialize(string inputFolder)
    {
        const int bufferSize = 64 * 1024 * 1024;
        int counter = 0;
        var records = new List<MetaData>();

        byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            var fs = new FileStream(Path.Combine(inputFolder, "meta.dani"), FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: false);
            var reader = new SpanStreamReader(fs, sharedBuffer);

            while (reader.HasData)
            {
                reader.EnsureBuffer();

                var meta = new MetaData();
                int localCursor = 0;
                meta.Read(reader.CurrentSpan, ref localCursor);
                records.Add(meta);
                reader.Advance(localCursor);

                var c = Interlocked.Increment(ref counter);
                if (c % 100 == 0) Console.WriteLine($"Desered {c}...");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sharedBuffer);
        }

        return records;
    }
}
