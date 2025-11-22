using System.Text.Json;
using JsonToMidiConverter;
using JsonToMidiConverter.Models.Song;
using JsonToMidiConverter.Test;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

var songPairs = new Dictionary<string, string>
{
    [@"References\LinkinPark.mid"] = "In the end",
    [@"References\Nirvana.mid"] = "Come as you are",
};

var pair = songPairs.ElementAt(0);

var match = Database.Search(pair.Value).First();
var data = Database.GetMidiData(match.SongId);
var song = JsonSerializer.Deserialize<Song>(data, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
});

var mid = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(15360) };
Time.Map = song.Parts[0].GetTempo(mid);
mid.ReplaceTempoMap(Time.Map);
song.Build();


DebugShit.CheckConsistency(pair.Key, song);
DebugShit.WriteDebugFile(song);

return;
var midiFile = Converter.Convert(song, pair.Key);

midiFile.Write("Output.mid", overwriteFile: true);