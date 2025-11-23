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


var pair = songPairs.ElementAt(1);

var match = Database.Search(pair.Value).First();
var song = Database.GetMidiData(match.SongId);

var mid = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(15360) };
Time.Map = song.Parts[0].GetTempo(mid);
mid.ReplaceTempoMap(Time.Map);
song.Build();

//var midiFile = Converter.Convert(song, pair.Key);
//midiFile.Write("Output.mid", overwriteFile: true);

Dumper.DumpJsonInputs(match.SongId);

foreach (var kvp in songPairs)
{

    var dumpMatch = Database.Search(kvp.Value).First();
    var dumpSong = Database.GetMidiData(dumpMatch.SongId);

    var dumpMid = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(15360) };
    Time.Map = dumpSong.Parts[0].GetTempo(mid);
    dumpMid.ReplaceTempoMap(Time.Map);
    dumpSong.Build();
    
    
    Dumper.Dump(dumpSong, kvp.Key, dumpMatch.Artist!);
}

