using JsonToMidiConverter.Models;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Core;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace JsonToMidiConverter.Test;

public static class Dumper
{
    public const string DumperRoot = "Dumper";

    public static readonly IReadOnlyDictionary<Type, JsonSerializerOptions> JsonOptions =
        new Dictionary<Type, JsonSerializerOptions>
        {
            [typeof(Measure)] = GetTypeExcludedJsonOptions(nameof(Measure.Voices)),
            [typeof(Beat)] = GetTypeExcludedJsonOptions(nameof(Beat.Notes), nameof(Beat.Text)),
            [typeof(Part)] = GetTypeExcludedJsonOptions(nameof(Part.NewLyrics), nameof(Part.Measures), nameof(Part.Automations)),
            [typeof(Voice)] = GetTypeExcludedJsonOptions(nameof(Voice.Beats)),
        };

    public static void Dump(Song song, MidiFile midi, RecordModel record, bool overwrite)
    {
        WriteMinifiedMidiInputJson(record, overwrite);
        WriteRawMidiData(song, midi, record, overwrite);
        WriteMeasuredMidiData(song, midi, record, overwrite);
        AssignNotesToMidiEvents(song, midi);
        WriteBible(song, midi, record, overwrite);

        Console.WriteLine($"Dumped: {record.Artist} - {record.Title}");
    }

    public static void WriteBible(Song song, MidiFile midi, RecordModel record, bool overwrite)
    {
        if (!overwrite && File.Exists(GetFileName("Bible", record))) return;

        var sb = new StringBuilder();
        foreach (var part in song.Parts)
        {
            var partTitle = $"================================== P{part.Index} - I{part.InstrumentId}: {part.Instrument} {part.Name} ==================================";
            var separator = new string(Enumerable.Range(0, partTitle.Length).Select(_ => '-').ToArray());

            sb.AppendLine(separator);
            sb.AppendLine(partTitle);
            sb.AppendLine(separator);

            foreach (var measure in part.Measures.Where(e => !e.Rest))
            {
                sb.AppendLine($"\r\n\r\n{measure}, Input = {GetJson(measure)}");
                foreach (var voice in measure.Voices)
                {
                    sb.AppendLine($"\r\n\r\n{voice}, Input = {GetJson(voice)}");
                    foreach (var beat in voice.Beats.Where(e => !e.Rest))
                    {
                        sb.AppendLine($"\r\n\t{beat}, Starts: {beat.AbsoluteBeatStartTime.Tick}, Attr = [{GetAttributes(beat)}], Input = {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {
                            var slideMarker = note.Slides.Count > 0  ? $" Slide = [{string.Join(", ", note.Slides)}]" : "";
                            var tieMarker = note.Tie ? " Tie " : "";

                            sb.AppendLine($"\t\t{note} {slideMarker}{tieMarker} CH {note.Channel}, NN {note.NoteNumber} Dur: {note.ActualDuration.Tick}, Attr = [{GetAttributes(beat)}] Input = {GetJson(note)}");

                            foreach (var midiNoteEvent in note.MidiNoteEvents)
                            {
                                sb.AppendLine($"\t\t\t {GetMidiEventString(midiNoteEvent)}");
                            }

                            if (note.MidiNoteEvents.Count > 1)
                            {

                                var midiEvents = note.MidiNoteEvents;

                                var holdNoteEvent = midiEvents.First();
                                var holdDuration = holdNoteEvent.Duration;
                                var stepNoteEvents = midiEvents.Where(e => e != holdNoteEvent).ToList();
                                var slideStartsAt = stepNoteEvents.Min(e => e.Start);
                                var slideEndsAt = stepNoteEvents.Max(e => e.End);
                                var totalSlideDuration = slideEndsAt - slideStartsAt;
                                var slideStepCount = stepNoteEvents.Count;
                                var totalDuration = midiEvents.Max(e => e.End) - midiEvents.Min(e => e.Start);

                                //var tiedDuration = note.Tie ? note.TieDetails!.FullDuration.Tick : note.ActualDuration.Tick;

                                var isOverlapping = holdNoteEvent.End > slideStartsAt;

                                var holdSlideRatio = (double)holdDuration / totalSlideDuration;
                                //var slideTiedRatio = (double)totalSlideDuration / tiedDuration;

                                sb.AppendLine($"\t\t\t ----- Overlap: {isOverlapping}, Total: {totalDuration}, Hold: {holdDuration}, slide: {totalSlideDuration}, Steps: {slideStepCount}, Hold-slide: {holdSlideRatio:P2}");
                            }

                        }
                    }
                }
            }
        }

        Save(sb.ToString(), "Bible", record, overwrite);
    }

    public static void AssignNotesToMidiEvents(Song song, MidiFile midi)
    {
        foreach (var part in song.Parts)
        {
            Time.Map = part.TempoMap;

            var originalEvents = midi.GetEvents(part.Index);
            var measureEvents = originalEvents.ToList();

            var notes = part.Measures
                .SelectMany(e => e.Voices)
                .SelectMany(e => e.Beats)
                .SelectMany(n => n.Notes)
                .Where(e => !e.Rest)
                .ToList();

            foreach(var note in notes)
            {
                if (note.Slides.Count > 0 && note.Tremolo.Count > 0)
                    throw new Exception("Just drop this track in the bin doesnt matter fuck that");

                var emittedNotes = note.GetEmittedNotes().ToList();

                foreach (var emittedNote in emittedNotes)
                {
                    var noteEvent = measureEvents
                        .SkipWhile(e => e.On.Channel != note.Channel || e.On.NoteNumber != emittedNote)
                        .First();

                    measureEvents.Remove(noteEvent); 
                    note.MidiNoteEvents.Add(noteEvent);
                }

                if (note.LastInBeat && note.Beat.LastInMeasure)
                {
                    Debug.Assert(measureEvents.All(e => e.MeasureIndex >= note.Measure.Index));
                }
            }
            Debug.Assert(measureEvents.Count == 0);
        }
    }

    private static void WriteMinifiedMidiInputJson(RecordModel record, bool overwrite)
    {
        if (!overwrite && File.Exists(GetFileName("JsonMini", record))) return;

        var song = Database.GetMidiData(record.SongId);
        var sb = new StringBuilder();
        foreach (var part in song.Parts)
        {
            sb.AppendLine($"\t P{part.Index} {part.Name} {part.Instrument} I{part.InstrumentId}, Tempo changes:");
            foreach (var tempoChange in part.Automations.Tempo)
            {
                sb.AppendLine($"\t\t - {GetJson(tempoChange)}]");
            }
        }

        sb.AppendLine();
        sb.AppendLine();

        var p = 0;
        foreach (var part in song.Parts)
        {
            var m = 0;
            sb.AppendLine($"\r\n\r\nP{p} {GetJson(part)}");
            foreach (var measure in part.Measures)
            {
                var v = 0;
                sb.AppendLine($"\r\n\tM{m} P{p} {GetJson(measure)}");
                foreach (var voice in measure.Voices)
                {
                    var b = 0;
                    sb.AppendLine($"\r\n\t\tV{v} M{m} P{p} {GetJson(voice)}");
                    foreach (var beat in voice.Beats)
                    {
                        var n = 0;
                        sb.AppendLine($"\t\t\tB{b} V{v} M{m} P{p} Starts: {beat.AbsoluteBeatStartTime.Tick} {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {

                            sb.AppendLine($"\t\t\t\tN{n} B{b} V{v} M{m} P{p} Dur: {note.ActualDuration.Tick} {GetJson(note)}");
                            n++;
                        }

                        b++;
                    }

                    v++;
                }

                m++;
            }

            p++;
        }

        Save(sb.ToString(), "JsonMini", record, overwrite);
    }

    private static void WriteMeasuredMidiData(Song song, MidiFile midi, RecordModel record, bool overwrite)
    {
        if (!overwrite && File.Exists(GetFileName("MidiMeasured", record))) return;

        var sb = new StringBuilder();

        foreach (var part in song.Parts)
        {
            var partEvents = midi.GetEvents(part.Index);

            sb.AppendLine($"\r\n\r\n{part} {GetJson(part)}");
            foreach (var measure in part.Measures)
            {
                sb.AppendLine($"\r\n\t{measure} {GetJson(measure)}");
                foreach (var voice in measure.Voices)
                {
                    sb.AppendLine($"\r\n\t\t{voice} {GetJson(voice)}");
                    foreach (var beat in voice.Beats)
                    {
                        sb.AppendLine($"\t\t\t{beat} {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {
                            sb.AppendLine(
                                $"\t\t\t\t{note} {GetJson(note)}");
                        }
                    }
                }

                foreach (var timedEvent in partEvents)
                {
                    sb.AppendLine($"\t\t {GetMidiEventString(timedEvent)}");
                }
            }
        }

        Save(sb.ToString(), "MidiMeasured", record, overwrite);
    }

    private static void WriteRawMidiData(Song song, MidiFile midi, RecordModel record, bool overwrite)
    {
        if (!overwrite && File.Exists(GetFileName("MidiRaw", record))) return;

        var sb = new StringBuilder();

        foreach (var part in song.Parts)
        {
            var partEvents = midi.GetEvents(part.Index);
            foreach (var ev in partEvents)
            {
                sb.AppendLine($"\t\t {GetMidiEventString(ev)}");
            }
        }

        Save(sb.ToString(), "MidiRaw", record, overwrite);
    }

    private static string GetMidiEventString(TimedNoteEvent note)
    {
        var timing = $"Start: {note.Start.ToString(),6}; End: {note.End.ToString(),6}, Duration: {note.Duration.ToString(),5}";
        return $"{note.EventIndex.ToString(),5} On {note.On.NoteNumber,2}; {timing}; Ch: {note.On.Channel};";
    }

    public static string GetAttributes(object model)
    {
        return model switch
        {
            Nota note => string.Join(", ", note.Rest ? "Rest" : "", note.Tie ? "Tie" : ""),
            Beat beat => string.Join(", ", beat.Rest ? "Rest" : ""),

            _ => string.Empty
        };
    }

    public static string GetJson<T>(T model)
    {
        if (!JsonOptions.TryGetValue(typeof(T), out var options))
            options = new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };

        return JsonSerializer.Serialize(model, options);
    }

    private static JsonSerializerOptions GetTypeExcludedJsonOptions(params string[] excludedProperties)
    {
        var hash = excludedProperties.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo => typeInfo.Properties
                        .Where(e => hash.Contains(e.Name))
                        .ToList()
                        .ForEach(prop => typeInfo.Properties.Remove(prop))
                }
            }
        };
    }


    private static string GetFileName(string name, RecordModel record)
        => Path.Combine(DumperRoot, Database.CleanString(record.Artist!), Database.CleanString(record.Title!),
            name + ".js");

    private static void Save(string content, string name, RecordModel record, bool overwrite)
    {
        var title = Database.CleanString(record.Title!);
        var artist = Database.CleanString(record.Artist!);

        var folderRoot = Path.Combine(DumperRoot, artist, title);
        if (!Directory.Exists(folderRoot))
        {
            Directory.CreateDirectory(folderRoot);
        }

        var filePath = GetFileName(name, record);
        if (File.Exists(filePath))
        {
            if (!overwrite) return;
            if (File.ReadAllText(filePath) == content) return;
        }

        File.WriteAllText(filePath, content);
    }

}