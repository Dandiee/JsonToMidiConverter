using System.Text.Json;
using JsonToMidiConverter;



var match = Database.Search("In the end").First();
var data = Database.GetMidiData(match.SongId);
var song = JsonSerializer.Deserialize<Song>(data, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
});

var midiFile = MidiConverter.Convert(song, @"References\Nirvana.mid");
midiFile.Write("Output.mid", overwriteFile: true);