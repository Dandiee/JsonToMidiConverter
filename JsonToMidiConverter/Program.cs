using CsvHelper;
using JsonToMidiConverter;
using JsonToMidiConverter.Models.Song;
using JsonToMidiConverter.Test;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

//Database.TestAll();

await Database.DeserializeRawJsons();

//await Database.DeserEndToEnd();

//await Database.RefreshSong(27);

while (false)
{
    Console.Write("Whatcha looking for:");
    var filter = Console.ReadLine();
    Console.WriteLine("Looking for it...");
    var onlineResults = await Database.OnlineSearch(filter);

    foreach (var result in onlineResults.Records.Take(10))
    {
        Console.WriteLine($"[{result.SongId}] {result.Artist} - {result.Title}");
    }

    Console.WriteLine("Stealing track...");
    var best = onlineResults.Records.First();
    await Database.RefreshSong(best.SongId);

    Console.WriteLine("Processing midi...");
    var bestMidi = Database.GetMidiData(best.SongId);
    var bestSong = Database.Get(best.SongId);
    Dumper.DumpWithoutReference(bestMidi, bestSong, true);
    var output = new MidiFile { TimeDivision = Converter.Tpqn };
    Time.Map = bestMidi.Parts[0].GetTempo(output);
    output.ReplaceTempoMap(Time.Map);
    bestMidi.Build(output, bestSong);


    Converter.Convert(bestMidi, bestSong);
    Console.WriteLine("EZ GG WP");
}

var midis = Directory.GetFiles("FreshSongs");
var c = 0;
foreach (var midiPath in midis)
{
    var pathParts = midiPath.Split('-');
    var artist = pathParts[0];
    var title = string.Join("-", pathParts.Skip(1).Take(pathParts.Length - 4));

    var record = Database.Search(artist, title).First();
    //if (!record.Title.Contains("Californication"))
    if (!record.Title.Contains("Nothing Else Matters", StringComparison.OrdinalIgnoreCase))
    {
        //continue;
    }

    var song = Database.GetMidiData(record.SongId);

    var mid = new MidiFile { TimeDivision = Converter.Tpqn };
    Time.Map = song.Parts[0].GetTempo(mid);
    mid.ReplaceTempoMap(Time.Map);


    if (song.Parts.SelectMany(e => e.Measures).SelectMany(e => e.Voices).SelectMany(e => e.Beats)
        .SelectMany(e => e.Notes).Any(e => e.Tremolo != null))
    {
        //Console.WriteLine("Fuck this piece of shit.");
        //continue;
    }

    song.Build(mid, record);
    Converter.Convert(song, record);

    Console.WriteLine($"Processing completed for {record.Title} {record.Artist}");

    //var reference = GetNormalizedMidi(midiPath);
    //Dumper.DumpBeforeBuild(song, reference, record, true);




    //Dumper.Dump(song, reference, record, true);
    //Dumper.TestSlides(song, reference, record);
    c++;

}


var q = Dumper.Bends.Select(e => new
{
    NoteDuration = e.Note.Duration.Tick,
    OutputPitchBends = e.Event.PitchBends,
    InputData = e.Note.Beat.TremoloBar
});

var data = JsonSerializer.Serialize(q);
File.WriteAllText("PitchBendings.json", data);


var vels = Dumper.Velocities.ToHashSet();

using (var writer = new StreamWriter("velocities.csv"))
using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
{
    csv.WriteRecords(vels);
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