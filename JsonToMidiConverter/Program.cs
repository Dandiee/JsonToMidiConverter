using System.Text.Json;
using JsonToMidiConverter;
using JsonToMidiConverter.Models.Song;
using JsonToMidiConverter.Test;

var songPairs = new Dictionary<string, string>
{
    [@"References\LinkinPark.mid"] = "In the end",
    [@"References\Nirvana.mid"] = "Come as you are",
};

var pair = songPairs.ElementAt(1);

var match = Database.Search(pair.Value).First();
var data = Database.GetMidiData(match.SongId);
var song = JsonSerializer.Deserialize<Song>(data, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
});

DebugShit.CheckConsistency(pair.Key);
DebugShit.WriteDebugFile(song);

var midiFile = Converter.Convert(song, pair.Key);

midiFile.Write("Output.mid", overwriteFile: true);