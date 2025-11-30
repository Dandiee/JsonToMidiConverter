using System.Text.Json.Serialization;
using Melanchall.DryWetMidi.Core;

namespace JsonToMidiConverter.Models.Song;

public sealed partial class Song
{
    [JsonIgnore] public MidiFile Midi { get; private set; }

    public string Name { get; private set; }

    public void Build(MidiFile midi, RecordModel record)
    {
        Name = $"{record.Artist} {record.Title}";

        Midi = midi;
        Parts = Parts.OrderBy(e => e.PartId).ToArray();

        for (var i = 0; i < Parts.Length; i++)
        {
            Parts[i].Build(this, i);
        }
    }
}