using System.Buffers;
using System.Runtime.InteropServices;

namespace Dani.Data.Serialization;

public static class DaniSerializer
{
    public static List<T> DeserializeRange<T>(string file, long startPosition, int count) where T : Serializable, new()
    {
        const int bufferSize = 64 * 1024 * 1024;
        var sharedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        
        var models = new List<T>(count);

        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: false);
            fs.Seek(startPosition, SeekOrigin.Begin);

            var reader = new SpanStreamReader(fs, sharedBuffer);
            for (int i = 0; i < count; i++)
            {
                reader.EnsureBuffer();

                var model = new T();
                int localCursor = 0;

                model.Read(reader.CurrentSpan, ref localCursor);
                models.Add(model);

                reader.Advance(localCursor);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sharedBuffer);
        }

        return models;
    }

    public static List<T> Deserialize<T>(string file) where T : Serializable, new()
    {
        const int bufferSize = 64 * 1024 * 1024;
        var counter = 0;
        var models = new List<T>();

        var sharedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: false);
            var reader = new SpanStreamReader(fs, sharedBuffer);

            while (reader.HasData)
            {
                reader.EnsureBuffer();

                var model = new T();
                int localCursor = 0;
                model.Read(reader.CurrentSpan, ref localCursor);
                models.Add(model);
                reader.Advance(localCursor);

                var c = Interlocked.Increment(ref counter);
                if (c % 100 == 0) Console.WriteLine($"Deserialized {c}...");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sharedBuffer);
        }

        return models;
    }

    public static void SerializeHeader(List<UInt128> index, string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        var rawSpan = CollectionsMarshal.AsSpan(index);
        var byteSpan = MemoryMarshal.AsBytes(rawSpan);
        fs.Write(byteSpan);
    }

    public static List<UInt128> DeserializeHeader(string filePath)
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

    public static IEnumerable<(T Model, long Cursor)> Serialize<T>(Stream outputStream, IEnumerable<T> items)
        where T : Serializable
    {
        const int bufferSize = 1024 * 1024 * 64;
        const int safetyMargin = 1024 * 1024 * 33;

        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        var totalBytesWritten = 0;
        var counter = 0;

        try
        {
            var cursor = 0;

            foreach (var item in items)
            {
                if (cursor > bufferSize - safetyMargin)
                {
                    outputStream.Write(buffer, 0, cursor);
                    totalBytesWritten += cursor;
                    cursor = 0;
                }

                yield return new(item, totalBytesWritten + cursor);

                item.Write(buffer, ref cursor);

                Interlocked.Increment(ref counter);
                if (counter % 100 == 0) Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: Serialized {counter} models");
            }

            if (cursor > 0)
            {
                outputStream.Write(buffer, 0, cursor);
            }

            outputStream.Flush();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

}