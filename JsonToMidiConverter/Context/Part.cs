using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("P{Index} {Song.Name} {Instrument} {Name}")]
public sealed partial class Part
{
    [JsonIgnore] public int Index { get; private set; }
    [JsonIgnore] public Song Song { get; private set; }
    [JsonIgnore] public bool IsPianoLike { get; private set; }
    [JsonIgnore] public TempoMap TempoMap { get; private set; }
    [JsonIgnore] public string FullName { get; private set; }
    [JsonIgnore] public List<Nota> Notes { get; set; } = [];


    public void Build(Song song, int index)
    {
        TempoMap = GetTempo(song.Midi);
        Index = index;
        Song = song;
        IsPianoLike = PianoLikeInstruments.Contains(InstrumentId);
        FullName = $"{song.Name} {Instrument} {Name}";

        SetSignatures();
        FixBeats();

        Measures = UnrollRepeats();

        for (var i = 0; i < Measures.Count; i++)
        {
            Measures[i].SetNavigation(this, i);
        }

        ApplyTripletFeel();
        ProcessGraceClusters();

        Measures.ForEach(m => m.Build());

        Notes = Measures
            .SelectMany(e => e.Voices)
            .SelectMany(e => e.Beats)
            .SelectMany(e => e.Notes)
            .ToList();

        foreach (var measure in Measures)
        {
            foreach (var voice in measure.Voices)
            {
                foreach (var beat in voice.Beats)
                {
                    beat.SetTimes();
                }
            }
        }

        Notes.ForEach(e => e.SetTimings());
    }

    private void SetSignatures()
    {
        var signature = new Time();
        foreach (var measure in Measures)
        {
            if (measure.SignatureArray.Count == 2)
            {
                signature = new Time(measure.SignatureArray[0], measure.SignatureArray[1]);
            }
            measure.Signature = signature;
        }
    }

    public static readonly IReadOnlyDictionary<string, Time> SupportedSwings = new Dictionary<string, Time>
    {
        ["8th"] = new(1, 8),
        ["16th"] = new(1, 16),
    };

    public void ApplyTripletFeel()
    {
        Time? division = null;

        foreach (var measure in Measures)
        {
            if (measure.Is("M51 P2", "no one knows"))
            {

            }

            if (!string.IsNullOrEmpty(measure.TripletFeel))
            {
                division = measure.TripletFeel.Equals("off", StringComparison.InvariantCulture)
                    ? null
                    : SupportedSwings[measure.TripletFeel];
            }

            if (division == null) continue;
            var offset = division / 3.0;

            foreach (var voice in measure.Voices)
            {
                var cursor = new Time();
                foreach (var beat in voice.Beats)
                {
                    if (!string.IsNullOrEmpty(beat.GraceNote)) continue;

                    var start = cursor;
                    var end = start + beat.Duration;

                    cursor += beat.Duration;

                    if (start.Tick % division.Value.Tick == 0 && end.Tick % division.Value.Tick == 0)
                    {
                        var gridCellsCovered = beat.Duration / division.Value.Tick;
                        if (gridCellsCovered % 2 > 0)
                        {
                            var startingGridIndex = start.Tick / division.Value.Tick;
                            if (startingGridIndex % 2 == 0)
                            {
                                beat.Duration += offset.Value;
                                beat.Modifications.Add("Triplet long");
                            }
                            else
                            {
                                beat.Duration -= offset.Value;
                                beat.Modifications.Add("Triplet short");
                            }
                        }
                    }
                }

                ShortenEnd(voice, allowCreation: false);
            }
        }
    }

    public void ProcessGraceClusters()
    {
        foreach (var measure in Measures)
        {
            foreach (var voice in measure.Voices)
            {
                var clusters = new List<List<Beat>>();
                var currentCluster = new List<Beat>();

                foreach (var beat in voice.Beats)
                {
                    if (beat.GraceNote != null)
                    {
                        if (currentCluster.Count == 0)
                        {
                            currentCluster.Add(beat);
                        }
                        else if (currentCluster[0].GraceNote == beat.GraceNote)
                        {
                            currentCluster.Add(beat);
                        }
                        else
                        {
                            clusters.Add(currentCluster);
                            currentCluster = [beat];
                        }
                    }
                    else
                    {
                        if (currentCluster.Count > 0)
                        {
                            clusters.Add(currentCluster);
                            currentCluster = [];
                        }
                    }
                }

                if (currentCluster.Count > 0)
                {
                    clusters.Add(currentCluster);
                }

                foreach (var cluster in clusters)
                {
                    if (cluster.Count > 1)
                    {
                        var averageDuration = new Time((long)cluster.Average(e => e.Duration.Tick));
                        var unitDuration = averageDuration / cluster.Count;

                        foreach (var beat in cluster)
                        {
                            beat.Duration = unitDuration;
                            beat.Modifications.Add("Grace duration updated");
                        }
                    }


                    var head = cluster[0];
                    var tail = cluster[^1];
                    var clusterLength = cluster.Sum(e => e.Duration.Tick);

                    if (head.GraceNote == "beforeBeat")
                    {
                        if (head.Previous!.Duration.Tick / 2 <= clusterLength)
                        {
                            head.Previous.Duration /= 2;
                            var stepSize = head.Previous.Duration / cluster.Count;
                            foreach (var beat in cluster)
                            {
                                beat.Duration = stepSize;
                                beat.Modifications.Add("Grace duration updated");
                            }
                        }
                        else head.Previous.Duration -= clusterLength;
                    }
                    else if (head.GraceNote == "onBeat")
                    {
                        if (tail.Next!.Duration.Tick / 2 <= clusterLength)
                        {
                            tail.Next.Duration /= 2;
                            var stepSize = tail.Next.Duration / cluster.Count;
                            foreach (var beat in cluster)
                            {
                                beat.Duration = stepSize;
                                beat.Modifications.Add("Grace duration updated");
                            }
                        }
                        else tail.Next.Duration -= clusterLength;
                    }
                }
            }
        }
    }

    public void FixBeats()
    {
        var measureIndex = 0;
        foreach (var measure in Measures)
        {
            if (Anacrusis && measureIndex == 0) continue;

            if (Index == 2 && measureIndex == 81 && Song.Name.Contains("Bon Jovi You Give Love a Bad Name!"))
            {

            }

            var duration = measure.Signature;

            foreach (var voice in measure.Voices)
            {
                var sum = voice.Beats.Where(e => string.IsNullOrEmpty(e.GraceNote)).Sum(b => b.Duration.Tick);
                var error = sum - duration.Tick;

                if (Math.Abs(error) > 20)
                {
                    ShortenEnd(voice, measure, allowCreation: true);
                }
            }

            measureIndex++;
        }
    }

    private void ShortenEnd(Voice voice, Measure? measure = null, bool allowCreation = false)
    {
        var targetMeasure = measure ?? voice.Measure;

        var expectedDuration = targetMeasure.Signature;
        var actualDuration = voice.Beats.Where(e => string.IsNullOrEmpty(e.GraceNote)).Sum(e => e.Duration.Tick);
        var error = expectedDuration - actualDuration;

        if (error.Tick == 0) return;

        if (error.Tick > 0)
        {
            if (voice.Beats[^1].Rest || !allowCreation)
            {
                voice.Beats[^1].Duration += error;
            }
            else
            {
                voice.Beats.Add(new Beat
                {
                    DurationArray = [error.Span.Numerator, error.Span.Denominator],
                    Rest = true,
                    Modifications = { "Manually created" }
                });
            }
        }
        else
        {
            foreach (var beat in voice.Beats[^1].Backward())
            {
                var beatDuration = beat.Duration;
                if (beatDuration.Tick < Math.Abs(error.Tick))
                {
                    beat.Duration = new Time();
                    error += beatDuration;
                    beat.Modifications.Add("Zeroed");
                }
                else if (beatDuration.Tick >= error.Tick)
                {
                    beat.Duration = beatDuration + error;
                    beat.Modifications.Add($"Shorten by {error.Tick}");
                    break;
                }
            }
        }
    }

    public List<Measure> UnrollRepeats()
    {
        var measures = new List<Measure>();
        var repeats = new List<Measure>();

        var c = 0;
        foreach (var measure in Measures)
        {
            measure.OriginalIndex = c++;

            if (measure.RepeatStart || repeats.Count > 0)
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
                    var part1 = repeats
                        .TakeWhile(e => e.AlternateEnding.Length == 0 || e.AlternateEnding.Contains(i + 1))
                        .ToList();
                    var part2 = repeats
                        .Skip(part1.Count)
                        .SkipWhile(e => !e.AlternateEnding.Contains(i + 1))
                        .TakeWhile(e => e.AlternateEnding.Length == 0 || e.AlternateEnding.Contains(i + 1))
                        .ToList();

                    var parts = part1.Concat(part2).ToList();

                    measures.AddRange(parts.Select(repeat =>
                    {
                        var copy = repeat.Clone();
                        copy.RepeatIndex = i + 1;
                        return copy;
                    }));
                }

                repeats.Clear();
            }


        }

        return measures;

    }

    public TempoMap GetTempo(MidiFile midi)
    {
        var bpmChangeByMeasure = Automations.Tempo.GroupBy(e => e.Measure)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Last().Bpm);
        List<int> lastSignature = [];
        var lastBpm = -1;

        using var tempoMapManager = new TempoMapManager(midi.TimeDivision);

        for (var i = 0; i < Measures.Count; i++)
        {
            var measure = Measures[i];
            if (measure.SignatureArray.Count == 2)
            {
                lastSignature = measure.SignatureArray;
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