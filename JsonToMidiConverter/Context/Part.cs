using System.Diagnostics;
using System.Text.Json.Serialization;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("P{Index} {Instrument} {Name}")]
public sealed partial class Part
{
    [JsonIgnore] public int Index { get; private set; }
    [JsonIgnore]public Song Song { get; private set; }
    [JsonIgnore]public bool IsPianoLike { get; private set; }
    [JsonIgnore]public TempoMap TempoMap { get; private set; }

    public void Build(Song song, int index)
    {
        TempoMap = GetTempo(song.Midi);
        Index = index;
        Song = song;
        IsPianoLike = PianoLikeInstruments.Contains(InstrumentId);

        Measures = GetLinearMeasures();

        for (var i = 0; i < Measures.Count; i++)
        {
            Measures[i].SetNavigation(this, i);
        }

        Measures.ForEach(m => m.Build());

        var maximumVoiceChannelCount = Measures.Max(e => e.Voices.Count);
        Debug.Assert(Measures.All(e => e.Voices.Count <= 1 || e.Voices.Count == maximumVoiceChannelCount));
    }

    public List<Measure> GetLinearMeasures()
    {
        var measures = new List<Measure>();
        var repeats = new List<Measure>();

        foreach (var measure in Measures)
        {
            if (measure.RepeatStart)
            {
                repeats.Add(measure);
            }

            if (repeats.Count == 0)
            {
                measures.Add(measure);
            }

            if (measure.Repeat > 0)
            {
                for (var i = 0; i < measure.Repeat; i++)
                {
                    foreach (var repeat in repeats)
                    {
                        measures.Add(repeat.Clone());
                    }
                }

                repeats.Clear();
            }

           
        }

        return measures;

    }

    public TempoMap GetTempo(MidiFile midi)
    {
        var bpmChangeByMeasure = Automations.Tempo.ToDictionary(kvp => kvp.Measure, kvp => kvp.Bpm);
        List<int> lastSignature = [];
        var lastBpm = -1;

        using var tempoMapManager = new TempoMapManager(midi.TimeDivision);

        for (var i = 0; i < Measures.Count; i++)
        {
            var measure = Measures[i];
            if (measure.Signature.Count == 2)
            {
                lastSignature = measure.Signature;
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

    public static readonly HashSet<int> PianoLikeInstruments = new() { 0, 48, 1024, 67, 66 };

    public override string ToString() => $"P{Index}";
}