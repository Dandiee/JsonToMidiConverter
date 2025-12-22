using CsvHelper;
using JsonToMidiConverter;
using JsonToMidiConverter.Test;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Globalization;
using System.Text.Json;
using Dani.Data;
using JsonToMidiConverter.Models.Song;

//Database.Build(@"c:\src\data\");
var db = new Database(@"C:\src\data\summary");
var pp = db.GetParts(db.GetBest("one").SongId);

//Database.Idk();   

//await Database.ProcessBeats();

//Database.TestAll();

//await Database.DeserializeRawJsons();

//await Database.DeserEndToEnd();

//await Database.RefreshSong(27);

//while (false)
//{
//    Console.Write("Whatcha looking for:");
//    var filter = Console.ReadLine();
//    Console.WriteLine("Looking for it...");
//    var record = db.GetBest(filter);

//    //Dumper.DumpWithoutReference(bestMidi, bestSong, true);
//    var output = new MidiFile { TimeDivision = Converter.Tpqn };
//    Time.Map = bestMidi.Parts[0].GetTempo(output);
//    output.ReplaceTempoMap(Time.Map);
//    bestMidi.Build(output, bestSong);


//    Converter.Convert(bestMidi, bestSong);
//    Console.WriteLine("EZ GG WP");
//}

var midis = Directory.GetFiles("FreshSongs");
var c = 0;
foreach (var midiPath in midis)
{
    var fileName = Path.GetFileNameWithoutExtension(midiPath);
    var pathParts = fileName.Split('-');
    var artist = pathParts[0];
    var title = string.Join("-", pathParts.Skip(1).Take(pathParts.Length - 4));

    var record = db.Get(artist, title);
    if (record == null) continue;
    var parts = db.GetParts(record.SongId);

    Dumper.DumpBeforeBuild(parts.ToList(), MidiFile.Read(midiPath), record, true);

    var song = new Song(record, parts);
    Converter.Convert(song);

    Console.WriteLine($"Processing completed for {record.Title} {record.Artist}");

    //var reference = GetNormalizedMidi(midiPath);
    //Dumper.DumpBeforeBuild(song, reference, record, true);




    //Dumper.Dump(song, reference, record, true);
    //Dumper.TestSlides(song, reference, record);
    c++;

}



static MidiFile GetNormalizedMidi(string file)
{
    var midi = MidiFile.Read(file);
    // 1. Get current division
    var oldDivision = midi.TimeDivision as TicksPerQuarterNoteTimeDivision;
    if (oldDivision == null)
        throw new NotSupportedException("Current time division is not TicksPerQuarterNote.");

    var oldTpqn = oldDivision.TicksPerQuarterNote;
    var newTpqn = Converter.Tpqn.TicksPerQuarterNote;

    if (oldTpqn == newTpqn) return midi;

    var ratio = (double)newTpqn / oldTpqn;

    foreach (var track in midi.GetTrackChunks())
    {
        using var timedEventsManager = track.ManageTimedEvents();
        foreach (var timedEvent in timedEventsManager.Objects)
        {
            timedEvent.Time = (long)Math.Round(timedEvent.Time * ratio);

        }
    }

    midi.TimeDivision = Converter.Tpqn;

    return midi;
}