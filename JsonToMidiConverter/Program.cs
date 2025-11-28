using System.Text.Json;
using JsonToMidiConverter;
using JsonToMidiConverter.Models.Song;
using JsonToMidiConverter.Test;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Tools;



var songPairs = new Dictionary<string, string>
{
    [@"References\Linkin Park-In The End-11-12-2025(3).mid"] = "In the end",
    //[@"References\Nirvana.mid"] = "Come as you are",
    //[@"References\Tool.mid"] = "Pneuma",
    //[@"References\LedZeppelin.mid"] = "Stairway to heaven",
    //[@"References\Metallica.mid"] = "Nothing else matters",
    //[@"References\Blink182.mid"] = "What's my age",
    //[@"References\Rage Against The Machine-Killing in the Name-11-24-2025.mid"] = "Killing in the name",
    //[@"References\Red Hot Chili Peppers-Can't Stop-11-15-2025.mid"] = "Can't stop"
};

//await Database.RefreshSong(385697);


foreach (var pair in songPairs)
{
    var match = Database.Search(pair.Value).First();
    var song = Database.GetMidiData(match.SongId);

    var mid = new MidiFile { TimeDivision = Converter.Tpqn };
    Time.Map = song.Parts[0].GetTempo(mid);
    mid.ReplaceTempoMap(Time.Map);
    song.Build(mid);


    var reference = GetNormalizedMidi(pair.Key);
    //Converter.Convert(song, reference);
    //Dumper.Dump(song, reference, match);

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