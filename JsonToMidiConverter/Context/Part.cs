using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System;
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

    public void ApplyTripletFeel()
    {
        var measureCounter = 0;
        string tripletFeel = null;

        foreach (var measure in Measures)
        {
            switch (measure.TripletFeel)
            {
                case "8th":
                    tripletFeel = "8th";
                    break;
                case "off":
                    tripletFeel = null;
                    break;
                case null:
                    break;
                default: throw new Exception();
            }

            if (tripletFeel == "8th")
            {
                var oneBeat = new Time(1, measure.Sgntr.Denominator);
                var oneThird = oneBeat / 3.0;
                var long8th = oneThird * 2.0;
                var short8th = oneThird;
                var eights = new Time(1, 8);
                double step = new Time(1, 8).Tick;

                

                foreach (var voice in measure.Voices)
                {
                    if (voice.Is("M7 P8", "money"))
                    {

                    }

                    var cursor = new Time();
                    var swangNotes = 0;
                    foreach (var beat in voice.Beats)
                    {
                        if (!string.IsNullOrEmpty(beat.GraceNote)) continue;

                        if (beat.Is("M7 P8", "money"))
                        {

                        }

                        var start = cursor;
                        var duration = beat.GetDuration();
                        var end = start + duration;

                        if (start.Tick % eights.Tick == 0 && end.Tick % eights.Tick == 0)
                        {
                            var gridCellsCovered = duration / eights.Tick;
                            if (gridCellsCovered % 2 > 0)
                            {
                                var targetDuration = duration;
                                var startingGridIndex = start.Tick / eights.Tick;
                                if (startingGridIndex % 2 == 0)
                                {
                                    targetDuration += new Time(1, 24);
                                }
                                else
                                {
                                    targetDuration -= new Time(1, 24);
                                }

                                beat.Duration = [targetDuration.Span.Numerator, targetDuration.Span.Denominator];
                            }

                        }

                        

                        cursor += duration;
                    }

                    ShortenEnd(0, voice);

                    //if (swangNotes % 2 == 1)
                    //{
                    //    var leftover = eights - short8th;
                    //    ShortenEnd(leftover.Tick, voice);
                    //}
                }
            }

            measureCounter++;
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
                        var averageDuration = cluster.Average(e => e.GetDuration().Tick);
                        var avgMusicalDuration = TimeConverter.ConvertTo<MusicalTimeSpan>((long)averageDuration, TempoMap);
                        var oneDuration = avgMusicalDuration / cluster.Count;
                        foreach (var beat in cluster)
                        {
                            beat.OriginalDuration = beat.Duration.Select(e => e).ToList();
                            beat.Duration[0] = (int)oneDuration.Numerator;
                            beat.Duration[1] = (int)oneDuration.Denominator;
                        }
                    }


                    var head = cluster[0];
                    var tail = cluster[^1];
                    var clusterLength = cluster.Sum(e => e.GetDuration().Tick);

                    if (head.Is("B3 V0 M7 P8", "money"))
                    {

                    }

                    if (head.GraceNote == "beforeBeat")
                    {
                        var prev = head.Previous;
                        var prevDur = prev.GetDuration();
                        if (prevDur.Tick / 2 <= clusterLength)
                        {
                            prev.Duration[1] *= 2;
                            var stepSize = (prevDur / 2) / cluster.Count;
                            var clusterUnitDuration = TimeConverter.ConvertTo<MusicalTimeSpan>(stepSize.Tick, TempoMap);
                            foreach (var beat in cluster)
                            {
                                beat.OriginalDuration = beat.Duration.Select(e => e).ToList();
                                beat.Duration[0] = (int)clusterUnitDuration.Numerator;
                                beat.Duration[1] = (int)clusterUnitDuration.Denominator;
                            }
                        }
                        else
                        {
                            var newPrevDur = TimeConverter.ConvertTo<MusicalTimeSpan>(prevDur.Tick - clusterLength, TempoMap);
                            prev.OriginalDuration = prev.Duration.Select(e => e).ToList();
                            prev.Duration[0] = (int)newPrevDur.Numerator;
                            prev.Duration[1] = (int)newPrevDur.Denominator;
                        }
                    }
                    else if (head.GraceNote == "onBeat")
                    {
                        var next = tail.Next;
                        var nextDur = next.GetDuration();
                        if (nextDur.Tick / 2 <= clusterLength)
                        {
                            next.Duration[1] *= 2;
                            var stepSize = (nextDur / 2) / cluster.Count;
                            var clusterUnitDuration = TimeConverter.ConvertTo<MusicalTimeSpan>(stepSize.Tick, TempoMap);
                            foreach (var beat in cluster)
                            {
                                beat.OriginalDuration = beat.Duration.Select(e => e).ToList();
                                beat.Duration[0] = (int)clusterUnitDuration.Numerator;
                                beat.Duration[1] = (int)clusterUnitDuration.Denominator;
                            }
                        }
                        else
                        {
                            var newNextDur = TimeConverter.ConvertTo<MusicalTimeSpan>(nextDur.Tick - clusterLength, TempoMap);
                            next.OriginalDuration = next.Duration.Select(e => e).ToList();
                            next.Duration[0] = (int)newNextDur.Numerator;
                            next.Duration[1] = (int)newNextDur.Denominator;
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

                var sum = voice.Beats.Where(e => string.IsNullOrEmpty(e.GraceNote)).Sum(b => b.GetDuration().Tick);
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
        var actualDuration = voice.Beats.Where(e => string.IsNullOrEmpty(e.GraceNote)).Sum(e => e.GetDuration().Tick);
        var error = expectedDuration - actualDuration;


        if (error.Tick > 0)
        {
            var lastBeat = voice.Beats[^1];
            var duration = lastBeat.GetDuration() + error;
            lastBeat.Duration[0] = duration.Span.Numerator;
            lastBeat.Duration[1] = duration.Span.Denominator;
        }
        else
        {
            foreach (var beat in voice.Beats[^1].Backward())
            {
                //if (beat.Rest || beat.Notes.All(e => e.Tie))
                {
                    var beatDuration = beat.GetDuration().Tick;
                    if (beatDuration < Math.Abs(error.Tick))
                    {
                        beat.Duration = [0, 0];
                        error += beatDuration;
                    }
                    else if (beatDuration >= error.Tick)
                    {
                        var leftover = TimeConverter.ConvertTo<MusicalTimeSpan>(beatDuration + error.Tick, TempoMap);
                        beat.Duration = [(int)leftover.Numerator, (int)leftover.Denominator];
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