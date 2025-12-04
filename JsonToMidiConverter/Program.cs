using JsonToMidiConverter;
using JsonToMidiConverter.Test;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;


var midis = Directory.GetFiles("FreshSongs");
var c = 0;
foreach (var midiPath in midis)
{
    var pathParts = midiPath.Split('-');
    var artist = pathParts[0];
    var title = string.Join("-", pathParts.Skip(1).Take(pathParts.Length - 4));

    var record = Database.Search(artist, title).First();
    //await Database.RefreshSong(record.SongId); continue;

    var song = Database.GetMidiData(record.SongId);

    var mid = new MidiFile { TimeDivision = Converter.Tpqn };
    Time.Map = song.Parts[0].GetTempo(mid);
    mid.ReplaceTempoMap(Time.Map);
    song.Build(mid, record);

    var reference = GetNormalizedMidi(midiPath);
    Dumper.Dump(song, reference, record, false);
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