using System.Text.Json;
using JsonToMidiConverter;
using JsonToMidiConverter.Models.Song;
using JsonToMidiConverter.Test;

var match = Database.Search("In the end").First();
var data = Database.GetMidiData(match.SongId);
var song = JsonSerializer.Deserialize<Song>(data, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
});

var midi = @"References\LinkinPark.mid";

DebugShit.CheckConsistency(midi);
DebugShit.WriteDebugFile(song);

var midiFile = Converter.Convert(song, midi);

midiFile.Write("Output.mid", overwriteFile: true);