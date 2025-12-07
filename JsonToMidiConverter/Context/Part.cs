using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("P{Index} {Song.Name} {Instrument} {Name}")]
public sealed partial class Part
{
    [JsonIgnore] public int Index { get; private set; }
    [JsonIgnore] public Song Song { get; private set; }
    [JsonIgnore] public bool IsPianoLike { get; private set; }
    [JsonIgnore] public TempoMap TempoMap { get; private set; }
    [JsonIgnore] public string FullName { get; private set; }
    [JsonIgnore] public Time AnacrusisOffset { get; set; }
    [JsonIgnore] public List<Nota> Notes { get; set; } = [];


    public void Build(Song song, int index)
    {
        TempoMap = GetTempo(song.Midi);
        Index = index;
        Song = song;
        IsPianoLike = PianoLikeInstruments.Contains(InstrumentId);
        FullName = $"{song.Name} {Instrument} {Name}";

        for (var i = 0; i < Measures.Count; i++)
        {
            Measures[i].Sgntr = TempoMap.GetTimeSignatureAtTime(new BarBeatTicksTimeSpan(i));
        }

        FixBeats();

        Measures = UnfoldRepeats();

        for (var i = 0; i < Measures.Count; i++)
        {
            Measures[i].SetNavigation(this, i);

        }

        ApplyTripletFeel();
        ProcessGraceClusters();

        //        FixGracePeriods();

        Measures.ForEach(m => m.Build());

        Notes = Measures
            .SelectMany(e => e.Voices)
            .SelectMany(e => e.Beats)
            .SelectMany(e => e.Notes)
            .ToList();

        foreach (var beat in Measures
                     .SelectMany(e => e.Voices)
                     .SelectMany(e => e.Beats))
        {
            beat.SetTimes();
        }

        Notes.ForEach(e => e.SetTimings());

        var maximumVoiceChannelCount = Measures.Max(e => e.Voices.Count);
        Debug.Assert(Measures.All(e => e.Voices.Count <= 1 || e.Voices.Count == maximumVoiceChannelCount));
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
                    var duration = beat.Duration;
                    var end = start + duration;

                    cursor += duration;

                    if (start.Tick % division.Value.Tick == 0 && end.Tick % division.Value.Tick == 0)
                    {
                        var gridCellsCovered = duration / division.Value.Tick;
                        if (gridCellsCovered % 2 > 0)
                        {
                            var startingGridIndex = start.Tick / division.Value.Tick;
                            if (startingGridIndex % 2 == 0)
                            {
                                duration += offset.Value;
                            }
                            else
                            {
                                duration -= offset.Value;
                            }

                            beat.Duration = duration;
                        }
                    }
                }

                ShortenEnd(0, voice);
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
                    if (cluster[0].Is("B7 V0 M77 P1"))
                    {

                    }

                    if (cluster.Count > 1)
                    {
                        var averageDuration = cluster.Average(e => e.Duration.Tick);
                        var avgMusicalDuration = new Time((long)averageDuration);
                        var oneDuration = avgMusicalDuration / cluster.Count;
                        foreach (var beat in cluster)
                        {
                            beat.Duration = oneDuration;
                        }
                    }


                    var head = cluster[0];
                    var tail = cluster[^1];
                    var clusterLength = cluster.Sum(e => e.Duration.Tick);

                    if (head.Is("B3 V0 M7 P8", "money"))
                    {

                    }

                    if (head.GraceNote == "beforeBeat")
                    {
                        var prev = head.Previous;
                        var prevDur = prev.Duration;
                        if (prevDur.Tick / 2 <= clusterLength)
                        {
                            prev.Duration /= 2;
                            var stepSize = (prevDur / 2) / cluster.Count;
                            foreach (var beat in cluster)
                            {
                                beat.Duration = stepSize;
                            }
                        }
                        else
                        {
                            prev.Duration = prevDur- clusterLength;
                        }
                    }
                    else if (head.GraceNote == "onBeat")
                    {
                        var next = tail.Next;
                        var nextDur = next.Duration;
                        if (nextDur.Tick / 2 <= clusterLength)
                        {
                            next.Duration /= 2;
                            var stepSize = (nextDur / 2) / cluster.Count;
                            foreach (var beat in cluster)
                            {
                                beat.Duration = stepSize;
                            }
                        }
                        else
                        {
                            var newNextDur = TimeConverter.ConvertTo<MusicalTimeSpan>(nextDur.Tick - clusterLength, TempoMap);
                            next.Duration = nextDur- clusterLength;
                        }
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

            var signature = measure.Sgntr;
            var duration = new Time(signature.Numerator, signature.Denominator);

            foreach (var voice in measure.Voices)
            {
                if (voice.Is("M7 P8", "money"))
                {

                }

                var sum = voice.Beats.Where(e => string.IsNullOrEmpty(e.GraceNote)).Sum(b => b.Duration.Tick);
                var error = sum - duration.Tick;

                if (error > 20)
                {
                    ShortenEnd(error, voice, measure);
                }
            }


            measureIndex++;
        }
    }

    private void ShortenEnd(long duration1, Voice voice, Measure? measure = null)
    {
        if (Song.Name.Contains("money", StringComparison.OrdinalIgnoreCase))
        {

        }

        var targetMeasure = measure ?? voice.Measure;

        var expectedDuration = new Time(targetMeasure.Sgntr.Numerator, targetMeasure.Sgntr.Denominator);
        var actualDuration = voice.Beats.Where(e => string.IsNullOrEmpty(e.GraceNote)).Sum(e => e.Duration.Tick);
        var error = expectedDuration - actualDuration;


        if (error.Tick > 0)
        {
            var lastBeat = voice.Beats[^1];
            var duration = lastBeat.Duration + error;
            lastBeat.Duration = duration;
        }
        else
        {
            foreach (var beat in voice.Beats[^1].Backward())
            {
                //if (beat.Rest || beat.Notes.All(e => e.Tie))
                {
                    var beatDuration = beat.Duration;
                    if (beatDuration.Tick < Math.Abs(error.Tick))
                    {
                        beat.Duration = new Time();
                        error += beatDuration;
                    }
                    else if (beatDuration.Tick >= error.Tick)
                    {
                        beat.Duration= beatDuration + error;
                        break;
                    }
                }
                // else throw new Exception();
                // i think i have a proof, check N0 B5 V0 M73 P5 in greenday - Holiday, theres a 1/16 which gets shortened to 1/48 with triple feel 8ths
            }
        }
    }

    public List<Measure> UnfoldRepeats()
    {
        var measures = new List<Measure>();
        var repeats = new List<Measure>();
        var part = FullName;
        if (Song.SongId == 580)
        {

        }

        var c = 0;
        foreach (var measure in Measures)
        {
            measure.OriginalIndex = c++;

            if (measure.AlternateEnding.Length > 1)
            {
                throw new Exception("whats happening");
            }

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
                    var alternateEndings = new List<int>();
                    foreach (var repeat in repeats)
                    {
                        if (repeat.AlternateEnding.Length > 0)
                        {
                            alternateEndings.AddRange(repeat.AlternateEnding);
                        }

                        if (alternateEndings.Count == 0 || alternateEndings.Contains(i + 1))
                        {
                            measures.Add(repeat.Clone());
                        }
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