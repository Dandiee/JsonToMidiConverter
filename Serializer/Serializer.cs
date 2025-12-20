using Api.Models;
using System.Buffers;
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
                    try
                    {
                        using var inputStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress);

                        var part = JsonSerializer.Deserialize(
                            decompressionStream,
                            JsonContext.Default.Part
                        );

                        if (part == null) continue;

                        // Flush check BEFORE writing
                        if (cursor > BufferSize - SafetyMargin)
                        {
                            outputStream.Write(buffer, 0, cursor);
                            cursor = 0;
                        }

                        part.Write(buffer, ref cursor);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error processing {Path.GetFileName(file)}: {e.Message}");
                        // Decide: Skip file? Fail batch?
                        // For archive integrity, you might want to log this but continue.
                    }

                    var c = Interlocked.Increment(ref counter);
                    if (c % 100 == 0) Console.WriteLine($"Processed {c}...");
                }

                // --- INTEGRITY 3: THE FINAL FLUSH ---
                // Write remaining bytes in the buffer to disk.
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

        int bufferSize = 64 * 1024 * 1024;
        byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        int i = 0;
        try
        {

            foreach (var partFile in files)
            {
                using var fs = new FileStream(partFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 1, // We manage buffering, disable FileStream's small internal buffer
                    useAsync: false);

                var reader = new SpanStreamReader(fs, sharedBuffer);
                while (reader.HasData)
                {
                    reader.EnsureBuffer();

                    var part = new Part();

                    int localCursor = 0;
                    part.Read(reader.CurrentSpan, ref localCursor);
                    reader.Advance(localCursor);
                    Console.WriteLine(i++);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sharedBuffer);
        }
        
    }

    internal ref struct SpanStreamReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer;
        private int _validLength; // How many bytes in _buffer are valid data
        private int _offset;      // Current read position in _buffer

        public SpanStreamReader(Stream stream, byte[] buffer)
        {
            _stream = stream;
            _buffer = buffer;
            _offset = 0;
            _validLength = 0;

            // Initial Fill
            FillBuffer();
        }

        public bool HasData => _offset < _validLength || _stream.Position < _stream.Length;

        public ReadOnlySpan<byte> CurrentSpan => new ReadOnlySpan<byte>(_buffer, _offset, _validLength - _offset);

        public void Advance(int bytesConsumed)
        {
            _offset += bytesConsumed;

            // Safety check
            if (_offset > _validLength)
                throw new InvalidOperationException("Parser read past the end of the buffer! Buffer too small for object?");
        }

        public void EnsureBuffer()
        {
            if (_offset > _buffer.Length * 0.75)
            {
                CompactAndFill();
            }
            else if (_validLength - _offset == 0 && _stream.Position < _stream.Length)
            {
                CompactAndFill();
            }
        }

        private void FillBuffer()
        {
            // Read as much as possible into free space
            int freeSpace = _buffer.Length - _validLength;
            if (freeSpace > 0)
            {
                int read = _stream.Read(_buffer, _validLength, freeSpace);
                _validLength += read;
            }
        }

        private void CompactAndFill()
        {
            int remaining = _validLength - _offset;

            if (remaining > 0)
            {
                // Move remaining data to start of buffer
                // We use Span.CopyTo which handles overlaps correctly
                _buffer.AsSpan(_offset, remaining).CopyTo(_buffer);
            }

            _validLength = remaining;
            _offset = 0;

            FillBuffer();
        }
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
