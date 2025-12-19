using Api.Models;
using Persistence;
using Persistence.Models;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Serializer;


public class ThreadSafeIndexer
{
    private readonly ConcurrentDictionary<byte[], int> _lookup;
    private readonly ConcurrentDictionary<byte[], int>.AlternateLookup<ReadOnlySpan<byte>> _alternateLookup;
    private readonly List<byte[]> _values = [];
    private readonly Lock _writeLock = new();

    public ThreadSafeIndexer()
    {
        _lookup = new ConcurrentDictionary<byte[], int>(ByteSpanComparer.Instance);
        _alternateLookup = _lookup.GetAlternateLookup<ReadOnlySpan<byte>>();
    }

    public int Cached;
    public int All;

    public int GetOrAdd(ReadOnlySpan<byte> item)
    {
        Interlocked.Increment(ref All);

        if (_alternateLookup.TryGetValue(item, out var existingIndex))
        {
            return existingIndex;
        }

        lock (_writeLock)
        {
            if (_alternateLookup.TryGetValue(item, out existingIndex))
            {
                return existingIndex;
            }

            Interlocked.Increment(ref Cached);
            var newIndex = _lookup.Count;
            var array = item.ToArray();

            if (_lookup.TryAdd(array, newIndex))
            {
                _values.Add(array);
                return newIndex;
            }

            _alternateLookup.TryGetValue(item, out existingIndex);
            return existingIndex;
        }
    }

    public IReadOnlyList<byte[]> GetValueBuffer()
    {
        lock (_writeLock)
        {
            return _values;
        }
    }
}

public class PartSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public void Serialize(string dataFolder, string outputFolder)
    {
        ThreadSafeIndexer beats = new();
        ThreadSafeIndexer notes = new();
        var mdop = Environment.ProcessorCount;

        var files = Directory.GetFiles(dataFolder, $"*.gz").ToList();
        var chunks = files.Chunk(files.Count / mdop).ToList();

        var serializer = new Serializer(beats, notes);
        var counter = 0;
        //var index = 0;
        var sw = Stopwatch.StartNew();
        //foreach(var chunk in chunks)
        Parallel.ForEach(chunks, new ParallelOptions { MaxDegreeOfParallelism = mdop }, (chunk, _, index) =>
        {
            var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024 * 32);
            var span = buffer.AsSpan();

            var path = Path.Combine(outputFolder, $"parts_{index}.dani");
            if (File.Exists(path))
                File.Delete(path);

            using var outputStream = File.OpenWrite(Path.Combine(outputFolder, $"parts_{index}.dani"));

            var q1 = beats;
            var q2 = notes;

            try
            {
                foreach (var file in chunk)
                {
                    try
                    {
                        using var inputStream = File.OpenRead(file);
                        using var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress);
                        var part = JsonSerializer.Deserialize<RawPart>(decompressionStream, JsonOptions);
                        //var part = JsonSerializer.Deserialize(decompressionStream, JsonContext.Default.RawPart);
                        var length = serializer.Serialize(part, span);
                        outputStream.Write(span[..length]);

                        foreach (var beat in part.Measures.SelectMany(e => e.Voices).SelectMany(e => e.Beats))
                        {
                            foreach (var note in beat.Notes)
                            {
                                NoteFactory.FromRaw(note);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        using var inputStream = File.OpenRead(file);
                        using var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress);
                        using var dumpStream = File.Create(@"c:\src\data\Dump\dump.json");
                        decompressionStream.CopyTo(dumpStream);
                        throw;
                    }

                    Interlocked.Increment(ref counter);
                    if (counter % 100 == 0)
                    {
                        Console.WriteLine($"Processed {counter}...");
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        });//);

        sw.Stop();

        Console.WriteLine($"{(sw.ElapsedMilliseconds / 1000f):N1} sec");

        WriteBuffers(Path.Combine(outputFolder, "parts.beats.notes.dani"), notes);
        WriteBuffers(Path.Combine(outputFolder, "parts.beats.dani"), beats);
    }

    private static void WriteBuffers(string path, ThreadSafeIndexer buffer)
    {
        using var stream = File.OpenWrite(path);
        foreach (var item in buffer.GetValueBuffer())
        {
            stream.Write(item, 0, item.Length);
        }
    }
}

public sealed class Serializer(ThreadSafeIndexer beats, ThreadSafeIndexer notes)
{
    public int Serialize(object obj, Span<byte> buffer)
    {
        var cursor = 0;
        SerializeInternal(obj, obj.GetType(), buffer, ref cursor);
        return cursor;
    }

    private void SerializeInternal(object? value, Type type, Span<byte> buffer, ref int cursor)
    {
        var isList = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        if (isList && value is IList list)
        {
            if (value == null) throw new InvalidOperationException("Lists cannot be null");

            // Write List Count
            buffer[cursor++] = (byte)list.Count;
            //WriteInt32(buffer, ref cursor, list.Count);
            var itemType = type.GetGenericArguments()[0];

            foreach (var item in list)
            {
                if (item is RawBeat beat)
                {
                    BeatFactory.FromRaw(beat).Write(buffer, ref cursor);
                    foreach (var note in beat.Notes)
                    {
                        NoteFactory.FromRaw(note).Write(buffer, ref cursor);
                    }
                }
                else SerializeInternal(item, itemType, buffer, ref cursor);
            }

        }
        else if (type == typeof(string))
        {
            if (value == null)
            {
                buffer[cursor++] = 0; // Null marker
            }
            else
            {
                buffer[cursor++] = 1; // Presence marker
                var lengthPosition = cursor;
                cursor += 4;
                var bytesWritten = Encoding.UTF8.GetBytes((string)value, buffer[cursor..]);
                WriteInt32At(buffer, lengthPosition, bytesWritten);
                cursor += bytesWritten;
            }
        }
        else if (!type.IsValueType)
        {
            if (value == null)
            {
                buffer[cursor++] = 0; // null marker
            }
            else
            {
                buffer[cursor++] = 1; // Presence marker
                var def = ObjectDefinition.Get(type);

                // Pack primitives
                Pack(value, def, buffer, ref cursor);

                // Recurse complex
                foreach (var prop in def.ComplexTypes)
                {
                    SerializeInternal(prop.Getter(value), prop.Type, buffer, ref cursor);
                }
            }
        }
        else
        {
            // Inside a List<Primitive> or List<Enum>
            // Note: We do NOT bit-pack items in a list against each other, 
            // we treat them as individual byte-aligned entities.

            var primitive = PropertyDefinition.GetPrimitive(value!.GetType());
            var bits = primitive.Packer(value);
            WritePrimitiveBits(buffer, ref cursor, bits, primitive.SizeInBytes);
        }

        if (value is IPoolable poolable)
        {
            poolable.Return();
        }
    }

    private static void Pack(object obj, ObjectDefinition def, Span<byte> buffer, ref int cursor)
    {
        if (def.PackTypes.Count == 0) return;

        var span = buffer.Slice(cursor, def.TotalPackSizeByte);
        span.Clear();

        var currentBitIndex = 0;

        foreach (var prop in def.PackTypes)
        {
            var bits = prop.UlongGetter(obj);
            var bitCount = prop.Primitive.SizeInBits;
            while (bitCount > 0)
            {
                var byteIndex = currentBitIndex >> 3; // divide by 8
                var bitOffset = currentBitIndex & 7;  // modulo 8

                span[byteIndex] |= (byte)(bits << bitOffset);

                var bitsWritten = 8 - bitOffset;
                if (bitsWritten > bitCount)
                    bitsWritten = bitCount;

                bits >>= bitsWritten;
                bitCount -= bitsWritten;
                currentBitIndex += bitsWritten;
            }
        }

        cursor += def.TotalPackSizeByte;
    }

    // --- Serialization Helpers (Little Endian) ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt32(Span<byte> buffer, ref int cursor, int value)
    {
        buffer[cursor++] = (byte)value;
        buffer[cursor++] = (byte)(value >> 8);
        buffer[cursor++] = (byte)(value >> 16);
        buffer[cursor++] = (byte)(value >> 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt64(Span<byte> buffer, ref int cursor, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(cursor), value);
        cursor += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt24(Span<byte> buffer, ref int cursor, int value)
    {
        buffer[cursor++] = (byte)value;
        buffer[cursor++] = (byte)(value >> 8);
        buffer[cursor++] = (byte)(value >> 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt16(Span<byte> buffer, ref int cursor, int value)
    {
        buffer[cursor++] = (byte)value;
        buffer[cursor++] = (byte)(value >> 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt24At(Span<byte> buffer, int position, int value)
    {
        buffer[position] = (byte)value;
        buffer[position + 1] = (byte)(value >> 8);
        buffer[position + 2] = (byte)(value >> 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt32At(Span<byte> buffer, int position, int value)
    {
        buffer[position] = (byte)value;
        buffer[position + 1] = (byte)(value >> 8);
        buffer[position + 2] = (byte)(value >> 16);
        buffer[position + 3] = (byte)(value >> 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WritePrimitiveBits(Span<byte> buffer, ref int cursor, ulong bits, int byteCount)
    {
        for (var i = 0; i < byteCount; i++)
        {
            buffer[cursor++] = (byte)(bits >> (i * 8));
        }
    }
}