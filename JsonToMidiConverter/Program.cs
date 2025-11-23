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

var midiFile = Converter.Convert(song, pair.Key);

foreach (var kvp in songPairs)
{

    var dumpMatch = Database.Search(kvp.Value).First();
    var dumpData = Database.GetMidiData(dumpMatch.SongId);
    var dumpSong = JsonSerializer.Deserialize<Song>(dumpData, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    var dumpMid = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(15360) };
    Time.Map = dumpSong.Parts[0].GetTempo(mid);
    dumpMid.ReplaceTempoMap(Time.Map);
    dumpSong.Build();
    
    midiFile.Write("Output.mid", overwriteFile: true);
    Dumper.Dump(dumpSong, kvp.Key, dumpMatch.Artist!);
}

