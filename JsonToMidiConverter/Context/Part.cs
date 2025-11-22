using System.Diagnostics;
using System.Text.Json.Serialization;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("P{Index}")]
public sealed partial class Part
{
    [JsonIgnore] public int Index { get; private set; }
    [JsonIgnore]public Song Song { get; private set; }
    [JsonIgnore]public bool IsPianoLike { get; private set; }
    [JsonIgnore]public TempoMap TempoMap { get; private set; }

    public void Build(Song song, int index)
    {
        Index = index;
        Song = song;
        IsPianoLike = PianoLikeInstruments.Contains(instrumentId);

        for (var i = 0; i < measures.Length; i++)
        {
            measures[i].Build(this, i);
        }
    }


    public TempoMap GetTempo(MidiFile midi)
    {
        var bpmChangeByMeasure = automations.tempo.ToDictionary(kvp => kvp.measure, kvp => kvp.bpm);
        int[] lastSignature = [];
        var lastBpm = -1;

        using var tempoMapManager = new TempoMapManager(midi.TimeDivision);

        for (var i = 0; i < measures.Length; i++)
        {
            var measure = measures[i];
            if (measure.signature.Length == 2)
            {
                lastSignature = measure.signature;
            }

            if (bpmChangeByMeasure.TryGetValue(i, out var newBpm))
            {
                lastBpm = newBpm;
            }

            var time = new BarBeatTicksTimeSpan(i, 0, 0);
            tempoMapManager.SetTimeSignature(time, new TimeSignature(lastSignature[0], lastSignature[1]));
            tempoMapManager.SetTempo(time, Tempo.FromBeatsPerMinute(lastBpm));
        }

        return tempoMapManager.TempoMap;
    }

    public static readonly HashSet<int> PianoLikeInstruments = new() { 0, 48, 1024 };
}