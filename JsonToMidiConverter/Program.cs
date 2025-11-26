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
    //[@"References\LedZeppelin.mid"] = "Stairway to heaven",
    [@"References\Tool.mid"] = "Pneuma",
    //[@"References\Metallica.mid"] = "Nothing else matters",
    //[@"References\Blink182.mid"] = "What's my age",
};


foreach (var pair in songPairs)
{
    var match = Database.Search(pair.Value).First();
    var song = Database.GetMidiData(match.SongId);

    var mid = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(15360) };
    Time.Map = song.Parts[0].GetTempo(mid);
    mid.ReplaceTempoMap(Time.Map);
    song.Build(mid);

    var midiFile = Converter.Convert(song, pair.Key);

    //var dumpMatch = Database.Search(asd.Value).First();
    // Dumper.DumpJsonInputs(dumpMatch.SongId, asd.Key);


}


foreach (var kvp in songPairs)
{

    var dumpMatch = Database.Search(kvp.Value).First();
    var dumpSong = Database.GetMidiData(dumpMatch.SongId);

    var dumpMid = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(15360) };
    Time.Map = dumpSong.Parts[0].GetTempo(dumpMid);
    dumpMid.ReplaceTempoMap(Time.Map);
    dumpSong.Build(dumpMid);
    
    
    //Dumper.Dump(dumpSong, kvp.Key, dumpMatch.Artist!);
    //Dumper.CollectSlide(dumpSong, kvp.Key, dumpMatch.Artist!);
}

