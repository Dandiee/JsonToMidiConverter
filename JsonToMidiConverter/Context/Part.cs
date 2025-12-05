using System.Diagnostics;
using System.Text.Json.Serialization;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

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

        ApplyTripletFeel();

        Measures = UnfoldRepeats();

        for (var i = 0; i < Measures.Count; i++)
        {
            Measures[i].SetNavigation(this, i);
        }

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
                if (measureCounter == 134 && Index == 1)
                {



                }

                var eightsCounter = 0;



                foreach (var voice in measure.Voices)
                {
                    var eightNotes = voice.Beats
                        .Where(e => e.Duration[0] == 1 && e.Duration[1] == 8)
                        .Where(e => !e.Notes.All(w => w.Staccato))
                        .ToList();
                    foreach (var eights in eightNotes)
                    {
                        eights.OriginalDuration = eights.Duration.Select(e => e).ToList();

                        if (eightsCounter % 2 == 0)
                        {
                            eights.Duration[1] = 6;
                        }
                        else
                        {
                            eights.Duration[1] = 12; // 1/12 -> 2/24 || 1/8 -> 3/24 -> 1/24 error
                        }

                        eightsCounter++;
                    }

                    if (eightNotes.Count % 2 == 1)
                    {
                        var last = voice.Beats[^1];
                        var duration = new MusicalTimeSpan(last.Duration[0], last.Duration[1]);
                        var error = new MusicalTimeSpan(1, 24);
                        var compensated = duration - error;

                        last.Duration[0] = (int)compensated.Numerator;
                        last.Duration[1] = (int)compensated.Denominator;
                    }
                }
            }

            measureCounter++;
        }
    }

    public void FixBeats()
    {
        foreach (var measure in Measures)
        {
            if (Anacrusis && measure.Index == 0) return;

            var signature = measure.Sgntr;
            var duration = new Time(signature.Numerator, signature.Denominator);

            foreach (var voice in measure.Voices)
            {
                var sum = voice.Beats.Where(e => string.IsNullOrEmpty(e.GraceNote)).Sum(b => b.GetDuration().Tick);
                var error = sum - duration.Tick;

                if (error > 10)
                {
                    foreach (var beat in voice.Beats[^1].Backward())
                    {
                        if (beat.Rest || beat.Notes.All(e => e.Tie))
                        {
                            var beatDuration = beat.GetDuration().Tick;
                            if (beatDuration < error)
                            {
                                beat.Duration = [0, 0];
                                error -= beatDuration;
                            }
                            else if (beatDuration >= error)
                            {
                                var leftover = TimeConverter.ConvertTo<BarBeatFractionTimeSpan>(beatDuration - error, TempoMap);
                                beat.Duration = [(int)leftover.Beats, (int)leftover.Bars];
                                break;
                            }
                        }
                        else throw new Exception();
                    }
                }
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