using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace Serializer;

public class PartSerializer
{
    public void Serialize(string dataFolder, string outputFolder)
    {
        var mdop = Environment.ProcessorCount;
        var files = Directory.GetFiles(dataFolder, $"*.gz").ToList();
        var chunks = files.Chunk(files.Count / mdop).ToList();

        var counter = 0;
        var index = 0;
        var sw = Stopwatch.StartNew();
        Parallel.ForEach(chunks, new ParallelOptions { MaxDegreeOfParallelism = mdop }, (chunk, _, index) =>
        {
            const int BufferSize = 1024 * 1024 * 64;
            const int SafetyMargin = 1024 * 1024 * 33; // Assume max object size is ~33MB
            const int StreamBufferSize = 128 * 1024; // Assume max object size is ~33MB

            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            var cursor = 0;
            var path = Path.Combine(outputFolder, $"parts_{index}.dani");
            using var outputStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan);
            try
            {
                foreach (var file in chunk)
                {
                    try
                    {
                        using var inputStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferSize);
                        using var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress);
                        //var part = JsonSerializer.Deserialize<Part>(decompressionStream, JsonOptions);
                        var part = JsonSerializer.Deserialize(
                            decompressionStream,
                            JsonContext.Default.Part
                        );
                        if (cursor > BufferSize - SafetyMargin)
                        {
                            outputStream.Write(buffer, 0, cursor);
                            cursor = 0;
                        }

                        part!.Write(buffer, ref cursor);
                    }
                    catch (Exception e)
                    {
                        //using var inputStream = File.OpenRead(file);
                        //using var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress);
                        //using var dumpStream = File.Create(@"c:\src\data\Dump\dump.json");
                        //decompressionStream.CopyTo(dumpStream);
                        //throw;

                        Console.WriteLine("Errorka...");
                    }

                    var c = Interlocked.Increment(ref counter);
                    if (c % 100 == 0) Console.WriteLine($"Processed {c}...");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        });//);

        
        sw.Stop();

        Console.WriteLine($"{(sw.ElapsedMilliseconds / 1000f):N1} sec");
    }
}
