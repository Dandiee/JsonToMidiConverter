using JsonToMidiConverter.Context;
using JsonToMidiConverter.Models.Song.Enums;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("P{Index} {Song.Name} {Instrument} {Name}")]
public sealed class Part : PartRaw
{
    public Part(PartRaw raw)
    {
        this.Bootstrap(raw);
        Measures = raw.MeasuresRaw.Select(e => new Measure(e)).ToList();
    }

    public int Index { get; private set; }
    public Song Song { get; private set; }
    public bool IsPianoLike { get; private set; }
    public TempoMap TempoMap { get; private set; }
    public string FullName { get; private set; }
    public List<Nota> Notes { get; set; } = [];
    public List<TimedEvent> TimedEvents { get; set; } = [];
    public List<Measure> Measures { get; set; } = [];

    public void Build(Song song, int index)
    {
        if (index == 2)
        {

        }
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

        ApplyBeatVelocities();

        Notes.ForEach(e => e.SetTimings());
    }

    private void ApplyBeatVelocities()
    {
        //var dict = new Dictionary<int, string>();
        var currentVelocity = Velocity.F;
        List<Beat> gradualVelocitySpan = [];

        foreach (var measure in Measures)
        {
            foreach (var voice in measure.Voices)
            {
                foreach (var beat in voice.Beats)
                {
                    if (beat.Velocity.HasValue)
                    {
                        currentVelocity = beat.Velocity.Value;
                    }

                    if (!beat.GradualVelocity.HasValue)
                    {
                        if (gradualVelocitySpan.Count == 0 || gradualVelocitySpan[0].GradualVelocity == beat.GradualVelocity)
                        {
                            gradualVelocitySpan.Add(beat);
                        }
                        else
                        {
                            ProcessGradualVelocity(ref gradualVelocitySpan);
                            gradualVelocitySpan = [beat];
                        }
                    }
                    else if (gradualVelocitySpan.Count > 0)
                    {
                        ProcessGradualVelocity(ref gradualVelocitySpan);
                    }

                    beat.CalculatedVelocity = currentVelocity;
                }
            }
        }

        if (gradualVelocitySpan.Count > 0)
        {
            ProcessGradualVelocity(ref gradualVelocitySpan);
        }
    }

    private static readonly List<Velocity> Velocities = [Velocity.Ppp, Velocity.Pp, Velocity.P, Velocity.Mp, Velocity.Mf, Velocity.F, Velocity.Ff, Velocity.Fff];
    private void ProcessGradualVelocity(ref List<Beat> span)
    {
        var start = span[0].CalculatedVelocity;
        var end = span[^1].Velocity ?? Velocity.F;

        var startIndex = Velocities.IndexOf(start);
        var endIndex = Velocities.IndexOf(end);

        var distance = endIndex - startIndex;
        var duration = span[^1].End - span[0].Start;

        //var stepDuration = duration / Math.Abs(distance);

        var cursor = span[0].Start;
        var firstBeat = span[0];
        foreach (var beat in span)
        {
            var diff = beat.Start - cursor;
            //var step = diff.Tick / stepDuration.Tick;
            //beat.CalculatedVelocity = Velocities[(int)(startIndex + step * Math.Sign(distance))];
            beat.GradualVelocityGroup = span;
        }

        span = [];
    }

    private void SetSignatures()
    {
        var signature = new Time();
        foreach (var measure in Measures)
        {
            if (measure.SignatureArray != null)
            {
                signature = new Time(measure.SignatureArray.Numerator, measure.SignatureArray.Denominator);
            }
            measure.Signature = signature;
        }
    }

    public static readonly IReadOnlyDictionary<TripletFeel, Time> SupportedSwings = new Dictionary<TripletFeel, Time>
    {
        [TripletFeel.Eights] = new(1, 8),
        [TripletFeel.Sixteen] = new(1, 16),
    };

    public void ApplyTripletFeel()
    {
        Time? division = null;

        foreach (var measure in Measures)
        {
            if (measure.TripletFeel.HasValue)
            {
                division = measure.TripletFeel == TripletFeel.Off
                    ? null
                    : SupportedSwings[measure.TripletFeel.Value];
            }

            if (division == null) continue;
            var offset = division / 3.0f;

            foreach (var voice in measure.Voices)
            {
                var cursor = new Time();
                foreach (var beat in voice.Beats)
                {
                    if (beat.GraceNote.HasValue) continue;

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

                    if (head.GraceNote == GraceNote.BeforeBeat)
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
                    else if (head.GraceNote == GraceNote.OnBeat)
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
                var sum = voice.Beats.Where(e => !e.GraceNote.HasValue).Sum(b => b.Duration.Tick);
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
        var actualDuration = voice.Beats.Where(e => !e.GraceNote.HasValue).Sum(e => e.Duration.Tick);
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
                voice.Beats.Add(new Beat(new BeatRaw
                {
                    DurationArray = new MusicalFraction
                    {
                        Numerator = (byte)error.Span.Numerator,
                        Denominator = (byte)error.Span.Denominator
                    },
                    Rest = true,
                })
                {
                    Modifications = { "Manually created" }
                });
            }
        }
        else
        {
            foreach (var beat in ((IMusicalElement<Beat>)voice.Beats[^1]).Backward())
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
                        .TakeWhile(e => e.AlternateEnding.Count == 0 || e.AlternateEnding.Contains((byte)(i + 1)))
                        .ToList();
                    var part2 = repeats
                        .Skip(part1.Count)
                        .SkipWhile(e => !e.AlternateEnding.Contains((byte)(i + 1)))
                        .TakeWhile(e => e.AlternateEnding.Count == 0 || e.AlternateEnding.Contains((byte)(i + 1)))
                        .ToList();

                    var parts = part1.Concat(part2).ToList();

                    measures.AddRange(parts.Select(repeat => new Measure(repeat.Clone())));
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
        MusicalFraction? lastSignature = null;
        var lastBpm = 120;

        using var tempoMapManager = new TempoMapManager(midi.TimeDivision);

        for (ushort i = 0; i < Measures.Count; i++)
        {
            var measure = Measures[i];
            if (measure.SignatureArray != null)
            {
                lastSignature = measure.SignatureArray;
            }

            if (bpmChangeByMeasure.TryGetValue(i, out var newBpm))
            {
                lastBpm = newBpm;
            }

            var time = new BarBeatTicksTimeSpan(i, 0, 0);
            tempoMapManager.SetTimeSignature(time, new TimeSignature(lastSignature.Numerator, lastSignature.Denominator));
            tempoMapManager.SetTempo(time, Melanchall.DryWetMidi.Interaction.Tempo.FromBeatsPerMinute(lastBpm));
        }

        return tempoMapManager.TempoMap;
    }

    public static readonly HashSet<int> PianoLikeInstruments = new() { 0, 48, 1024, 67, 66 };

    public override string ToString() => $"P{Index}";
}