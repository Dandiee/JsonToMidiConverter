using System.Diagnostics;
using Dani.Data.Models.Enums;
using Dani.Data.Models.Parts;
using Force.DeepCloner;
using Melanchall.DryWetMidi.Interaction;

using DataMeasure = Dani.Data.Models.Parts.Measure;
using DataPart = Dani.Data.Models.Parts.Part;


namespace Dani.Converter.Models;

[DebuggerDisplay("P{Index} {FullName}")]
public sealed class Part
{
    public Song Song { get; }
    public List<Nota> Notes { get; }

    public int Index { get; }
    public bool IsPianoLike { get; }
    public TempoMap TempoMap { get; }
    public string FullName { get; }
    public int InstrumentId { get; }
    public string Instrument { get; }
    public string Name { get; }
    public List<Measure> Measures { get; }
    public List<sbyte> Tuning { get; }
    public bool Anacrusis { get; }
    public int Capo { get; }
    public bool IsDrum { get; }

    public List<TimedEvent> TimedEvents { get; } = [];

    public Part(Song song, DataPart data)
    {
        TempoMap = GetTempo(data);
        Time.Map = TempoMap;

        Song = song;
        Index = data.Index;
        IsPianoLike = data.IsPianoLike();
        InstrumentId = data.InstrumentId;
        Instrument = data.Instrument;
        Name = data.Name;
        FullName = $"{song.Record.Artist} - {song.Record.Title}: {data.Instrument} / {data.Name}";
        Anacrusis = data.Anacrusis;
        Capo = data.Capo;
        IsDrum = data.InstrumentId == 1024;
        Tuning = data.Tuning;

        var unrolledMeasures = UnrollRepeats(data);
        Measures = new List<Measure>(unrolledMeasures.Count);
        for (var i = 0; i < data.Measures.Count; i++)
        {
            Measures.Add(new Measure(this, data.Measures[i], i));
        }
   
        FixBeats();
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
        Notes.ForEach(e => e.SecondPass());
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
                    if (beat.Velocity != Velocity.None)
                    {
                        currentVelocity = beat.Velocity;
                    }

                    if (beat.GradualVelocity == GradualVelocity.None)
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
        var end = span[^1].Velocity == Velocity.None ? Velocity.F : span[^1].Velocity;

        var startIndex = Velocities.IndexOf(start);
        var endIndex = Velocities.IndexOf(end);

        var distance = endIndex - startIndex;
        var duration = span[^1].End - span[0].Start;
        var stepDuration = duration / Math.Abs(distance);
        var cursor = span[0].Start;

        foreach (var beat in span)
        {
            var diff = beat.Start - cursor;
            var step = diff.Tick / stepDuration.Tick;
            beat.CalculatedVelocity = Velocities[(int)(startIndex + step * Math.Sign(distance))];
            beat.GradualVelocityGroup = span;
        }

        span = [];
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
            if (measure.TripletFeel != TripletFeel.None)
            {
                division = measure.TripletFeel == TripletFeel.Off
                    ? null
                    : SupportedSwings[measure.TripletFeel];
            }

            if (division == null) continue;
            var offset = division / 3.0f;

            foreach (var voice in measure.Voices)
            {
                var cursor = new Time();
                foreach (var beat in voice.Beats)
                {
                    if (beat.GraceNote != GraceNote.None) continue;

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
                            }
                            else
                            {
                                beat.Duration -= offset.Value;
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
                    if (beat.GraceNote != GraceNote.None)
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

            var duration = measure.Signature;

            foreach (var voice in measure.Voices)
            {
                var sum = voice.Beats.Where(e => e.GraceNote == GraceNote.None).Sum(b => b.Duration.Tick);
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
        var actualDuration = voice.Beats.Where(e => e.GraceNote == GraceNote.None).Sum(e => e.Duration.Tick);
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
                // TODO:
                // voice.Beats.Add(new Beat
                // {
                //     MusicalFraction = new MusicalFraction((byte)error.Span.Numerator, (byte)error.Span.Denominator),
                //     Rest = true,
                // });
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
                }
                else if (beatDuration.Tick >= error.Tick)
                {
                    beat.Duration = beatDuration + error;
                    break;
                }
            }
        }
    }

    public static List<DataMeasure> UnrollRepeats(DataPart part)
    {
        var measures = new List<DataMeasure>();
        var repeats = new List<DataMeasure>();

        foreach (var measure in part.Measures)
        {
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
                    var leadParts = repeats
                        .TakeWhile(e => e.AlternateEnding.Count == 0 || e.AlternateEnding.Contains((byte)(i + 1)))
                        .ToList();

                    var tailParts = repeats
                        .Skip(leadParts.Count)
                        .SkipWhile(e => !e.AlternateEnding.Contains((byte)(i + 1)))
                        .TakeWhile(e => e.AlternateEnding.Count == 0 || e.AlternateEnding.Contains((byte)(i + 1)))
                        .ToList();

                    var parts = leadParts.Concat(tailParts).ToList();

                    measures.AddRange(parts.Select(repeat => repeat.DeepClone()));
                }

                repeats.Clear();
            }


        }

        return measures;
    }

    private TempoMap GetTempo(DataPart data)
    {
        var bpmChangeByMeasure = data.Automations.Tempo
            .GroupBy(e => e.Measure)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Last().Bpm);

        MusicalFraction? lastSignature = null;
        var lastBpm = 120;

        using var tempoMapManager = new TempoMapManager(Converter.Tpqn);

        for (ushort i = 0; i < data.Measures.Count; i++)
        {
            var measure = data.Measures[i];
            if (!measure.Signature.IsZero())
            {
                lastSignature = measure.Signature;
            }

            if (bpmChangeByMeasure.TryGetValue(i, out var newBpm))
            {
                lastBpm = newBpm;
            }

            var time = new BarBeatTicksTimeSpan(i, 0, 0);
            tempoMapManager.SetTimeSignature(time, new TimeSignature(lastSignature!.Nominator, lastSignature.Denominator));
            tempoMapManager.SetTempo(time, Tempo.FromBeatsPerMinute(lastBpm));
        }

        return tempoMapManager.TempoMap;
    }

    

    public override string ToString() => $"P{Index}";
}