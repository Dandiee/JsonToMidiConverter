using System.Text.Json.Serialization;
using Melanchall.DryWetMidi.Core;

namespace JsonToMidiConverter.Models.Song;

public sealed class Song : SongRaw
{
    public MidiFile Midi { get; private set; }
    public string Name { get; private set; }
    public List<Part> Parts { get; private set; }

    public Song(SongRaw raw)
    {
        this.Bootstrap(raw);
        Parts = raw.PartsRaw.Select(e => new Part(e)).ToList();
    }

    public void Build(MidiFile midi, RecordModel record)
    {
        Name = $"{record.Artist} {record.Title}";

        Midi = midi;
        Parts = Parts.OrderBy(e => e.PartId).ToList();

        for (var i = 0; i < Parts.Count; i++)
        {
            Parts[i].Build(this, i);
        }
    }
}