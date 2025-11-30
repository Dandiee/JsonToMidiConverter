using System.Data;
using System.Text;
using System.Text.Json;
using JsonToMidiConverter;
using JsonToMidiConverter.Models;
using JsonToMidiConverter.Models.Song;
using JsonToMidiConverter.Test;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Tools;
using Slide = JsonToMidiConverter.Context.Slide;


var songPairs = new Dictionary<string, string>
{
    [@"References\Linkin Park-In The End-11-12-2025(3).mid"] = "In the end",
    [@"References\Nirvana.mid"] = "Come as you are",
    //[@"References\Tool.mid"] = "Pneuma",
    //    [@"References\LedZeppelin.mid"] = "Stairway to heaven",
    //[@"References\Metallica.mid"] = "Nothing else matters",
    [@"References\Rage Against The Machine-Killing in the Name-11-24-2025.mid"] = "Killing in the name",
    [@"References\Red Hot Chili Peppers-Can't Stop-11-15-2025.mid"] = "Can't stop"
};

//await Database.RefreshSong(385697);

var records = Database.GetTop50();

var midis = Directory.GetFiles("FreshSong");

foreach (var midiPath in midis)
{
    var pathParts = midiPath.Split('-');
    var artist = pathParts[0];
    var title = string.Join("-", pathParts.Skip(1).Take(pathParts.Length - 4));

    var record = Database.Search(artist, title).First();
    var song = Database.GetMidiData(record.SongId);

    //var midiFileName = $"{SanitizeRecordName(midiPath.Artist)}-{SanitizeRecordName(midiPath.Title)}-";
    //var midiFilePath = Directory.GetFiles("FreshSong", $"{midiFileName}*").Single();

    var mid = new MidiFile { TimeDivision = Converter.Tpqn };
    Time.Map = song.Parts[0].GetTempo(mid);
    mid.ReplaceTempoMap(Time.Map);
    song.Build(mid);

    var reference = GetNormalizedMidi(midiPath);
    //Dumper.TestSlides(song, reference, record);
}

var miniResults = Dumper.Cases
    .OrderByDescending(e => e.Slide)
    //.Where(e => !(e.StepDuration > 958 && e.StepDuration < 961))
    //.Where(e => e.HoldDuration != 0)
   
    //.OrderByDescending(e => e.SlideNoteRatio)
    .ToList();

var w = Dumper.Cases.MinBy(e => e.StepDuration);

var sb = new StringBuilder();
foreach (var res in miniResults)
{
    //sb.AppendLine($"{res.SlideRatio:P2} {res.Slide,-9} Steps: {res.Steps,2}, StepDur: {res.StepDuration,3}, HoldDur: {res.HoldDuration,5}, TotalDur: {res.TotalDuration,5}, Address: {res.Address,4}");
}

File.WriteAllText($"Slides_Mini.text", sb.ToString());
File.WriteAllText($"Slides_Mini.json", JsonSerializer.Serialize(miniResults, new JsonSerializerOptions(JsonSerializerDefaults.General)
{
    WriteIndented = true
}));

//await Database.RefreshTopsSong();

static string NormalizeMidiName(string name) => name.Replace("_", "/");
static string SanitizeRecordName(string name) => name.Replace("/", "_");

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