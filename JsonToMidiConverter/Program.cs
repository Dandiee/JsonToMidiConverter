
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using JsonToMidiConverter;
using Melanchall.DryWetMidi.Core;

const string inputFile = "Input.json";
const string referenceFile = "ReferenceOutput.mid";
const string outputFile = "Output.mid";

if (!File.Exists(inputFile))
{
    Console.Error.WriteLine($"Input file '{inputFile}' was not found.");
    return;
}

var json = File.ReadAllText(inputFile);
var song = JsonSerializer.Deserialize<Song>(json, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
});

if (song is null)
{
    Console.Error.WriteLine("Failed to parse song JSON.");
    return;
}

song.Build();

var midiFile = MidiConverter.Convert(song);
midiFile.Write(outputFile, overwriteFile: true);

if (File.Exists(referenceFile))
{
    var matchesReference = FilesEqual(outputFile, referenceFile);
    Console.WriteLine(matchesReference
        ? "Output.mid matches ReferenceOutput.mid byte-for-byte."
        : "Output.mid differs from ReferenceOutput.mid. Review conversion details.");

    if (!matchesReference)
    {
        MidiDiff.Report(referenceFile, outputFile);
    }
}
else
{
    Console.WriteLine("ReferenceOutput.mid not found. Skipped byte comparison.");
}

static bool FilesEqual(string firstPath, string secondPath)
{
    var firstBytes = File.ReadAllBytes(firstPath);
    var secondBytes = File.ReadAllBytes(secondPath);
    return firstBytes.Length == secondBytes.Length && firstBytes.SequenceEqual(secondBytes);
}