using JsonToMidiConverter.Context;
using JsonToMidiConverter.Models;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Data;
using System.Diagnostics;
using System.Net.Sockets;
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


    private static void CheckMeasureCounts(Song song, MidiFile midi)
    {
        var parts = midi.Chunks.OfType<TrackChunk>().ToList();

        foreach (var part in song.Parts)
        {

            var numberOfMidiMeasures = parts[part.Index].Events.OfType<MarkerEvent>().Count(e => e.Text.StartsWith("MEASURE_"));
            Debug.Assert(part.Measures.Count == numberOfMidiMeasures);

        }
    }

    public static void DumpBeforeBuild(Song song, MidiFile midi, RecordModel record, bool overwrite)
    {
        WriteMinifiedMidiInputJson(record, overwrite);
        WriteRawMidiData(midi, record, overwrite);
        WriteMeasuredMidiData(song, midi, record, overwrite);
        WriteMetaData(record, overwrite);
    }

    public static void Dump(Song song, MidiFile midi, RecordModel record, bool overwrite)
    {
        AssignNotesToMidiEvents(song, midi, record);
        CheckMeasureCounts(song, midi);
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
                        sb.AppendLine($"\r\n\t{beat}, Start: {beat.Start.Tick}, Attr = [{GetAttributes(beat)}], Input = {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {
                            var slideMarker = note.Slides.Count > 0 ? $" Slide = [{string.Join(", ", note.Slides)}]" : "";
                            var tieMarker = note.Tie ? " Tie " : "";

                            sb.AppendLine($"\t\t{note} {slideMarker}{tieMarker} CH {note.Channel}, NN {note.NoteNumber} Duration: {note.Duration.Tick}, Attr = [{GetAttributes(beat)}] Input = {GetJson(note)}");

                            foreach (var midiNoteEvent in note.MidiNoteEvents)
                            {
                                sb.AppendLine($"\t\t\t {GetMidiEventString(midiNoteEvent)}");

                                if (midiNoteEvent.PitchBends.Count > 0)
                                {
                                    foreach (var pitchBend in midiNoteEvent.PitchBends)
                                    {
                                        sb.AppendLine($"\t\t\t\t Value {pitchBend.Value}, Time: {pitchBend.Time}");
                                    }
                                }

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

    private static readonly (string Addres, string Track)[] StoppedCaringList =
    [
        //new ("V1 M249 P2", "ride")
    ];

    private static void OpenForDebug(Nota note, RecordModel record)
    {
        var paths = new[] { GetFileName("Bible", record), GetFileName("MidiMeasured", record) };
        var path = paths.First(File.Exists);
        var lines = File.ReadAllLines(path).ToList();
        var match = lines.Single(e => e.Contains($"{note}"));
        var index = lines.IndexOf(match);

        Process.Start(@"C:\Program Files\Notepad++\notepad++.exe", $"-n{index + 1} \"{path}\"");
        Process.Start(new ProcessStartInfo
        {
            FileName = $"https://www.songsterr.com/a/wsa/{GetUrlFriendlyName(record.Artist)}-{GetUrlFriendlyName(record.Title)}-tab-s{record.SongId}t{note.Part.Index}",
            UseShellExecute = true
        });

        var measureIndex = note.Part.Anacrusis
            ? note.Measure.OriginalIndex
            : note.Measure.OriginalIndex + 1;

        Console.WriteLine($"Error at {note} - Measure: {measureIndex}, Repeat: {note.Measure.RepeatIndex}");

    }

    public static string GetUrlFriendlyName(string name)
    {
        return name.ToLowerInvariant().Replace(" ", "-");
    }

    public record Velocity(
        int ResultVelocity,

        double Part_Balance,
        double Part_Volume,
        int Part_InstrumentId,

        string Beat_OriginalVelocity,
        string Beat_CalculatedVelocity,

        bool Beat_PalmMute,
        bool Beat_Vibrato,
        bool Beat_Tapping,
        string Beat_GraceNote,
        int Beat_UpStroke,
        int Beat_DownStroke,
        bool Beat_Slapping,
        bool Beat_Popping,
        string? Beat_GradualVelocity,
        string? Beat_VibratoWithTremoloBar,
        string? Beat_PickStroke,
        bool Beat_WideVibrato,
        int Beat_DownArpeggio,
        int Beat_UpArpeggio,

        bool Beat_IsPartOfBeamGroup,
        int? Beat_BeamGroupSize,
        int? Beat_IndexWithinBeamGroup,
        bool? Beat_BeamGroupContainsAccent,
        bool Beat_HasMultipleNotes,

        bool Note_Vibrato,
        bool Note_HammerOnPullOffTargetNote,
        bool Note_Tie,
        bool Note_Staccato,
        double Note_Acentuated,
        bool Note_Ghost,
        string? Note_Harmonic,
        bool Note_Dead,
        bool Note_WideVibrato
    );

    public static List<(TimedNoteEvent Event, Nota Note)> Bends = [];

    public static List<Velocity> Velocities = [];
    public static void AssignNotesToMidiEvents(Song song, MidiFile midi, RecordModel record)
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
                .OrderBy(e => e.Start.Tick)
                .ToList();

            foreach (var note in notes)
            {
                if (note.Slides.Count > 0 && note.Tremolo.Count > 0) throw new Exception("Just drop this track in the bin doesnt matter fuck that");

                var emittedNotes = note.GetEmittedNotes().ToList();

                var c = 0;
                foreach (var emittedNote in emittedNotes)
                {
                    var noteEvent = measureEvents
                        .SkipWhile(e => e.On.Channel != note.Channel || e.On.NoteNumber != emittedNote)
                        .First();

                    if (note.Bend != null && note.Beat.TremoloBar != null)
                    {
                        //OpenForDebug(note, record);
                    }

                    if (note.Bend != null)
                    {
                        //var pitchBends = note.GeneratePitchBends();
                    }

                    if (note.Beat.TremoloBar != null)
                    {
                        //Bends.Add(new (noteEvent, note));
                        var pitchBends = note.GenerateBends((int)note.Duration.Tick);
                    }

                    if (note.Bend == null && note.Beat.TremoloBar == null && note.Slides.Count > 0 && emittedNotes.Count > 1 && note.TieDetails == null && noteEvent.PitchBends.Count > 2)
                    {
                        OpenForDebug(note, record);
                    }


                    if (noteEvent.PitchBends.Count > 0 && note.Bend == null)
                    {
                        //OpenForDebug(note, record);
                    }

                    measureEvents.Remove(noteEvent);
                    note.MidiNoteEvents.Add(noteEvent);
                    c++;
                }

                if (note.LastInBeat && note.Beat.LastInMeasure)
                {
                    var allUsed = measureEvents.All(e => e.MeasureIndex >= note.Measure.Index);
                    Debug.Assert(allUsed);
                    if (!allUsed)
                    {
                        //OpenForDebug(note, record);
                    }
                }

            }
            Debug.Assert(measureEvents.Count == 0);
        }
    }

    private static bool ParticipatesInSlide(Nota note)
    {
        if (note.Slides.Count > 0) return true;
        if (note.Previous?.Slides.Contains(Slide.Legato) == true) return true;
        if (note.Next?.Slides.Contains(Slide.Below) == true) return true;

        return false;
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
                        sb.AppendLine($"\t\t\tB{b} V{v} M{m} P{p} Start: {beat.Start.Tick} {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {

                            sb.AppendLine($"\t\t\t\tN{n} B{b} V{v} M{m} P{p} Duration: {note.Duration.Tick} {GetJson(note)}");
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

    private static void WriteMetaData(RecordModel record, bool overwrite)
    {
        var meta = Database.GetMetaData(record.SongId);
        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            WriteIndented = true
        });

        Save(json, "Meta", record, overwrite);
    }

    private static void WriteMeasuredMidiData(Song song, MidiFile midi, RecordModel record, bool overwrite)
    {
        if (!overwrite && File.Exists(GetFileName("MidiMeasured", record))) return;

        var sb = new StringBuilder();

        var p = 0;
        foreach (var part in song.Parts)
        {
            var partEvents = midi.GetEvents(p);

            var m = 0;
            sb.AppendLine($"\r\n\r\n P{p} {GetJson(part)}");
            foreach (var measure in part.Measures)
            {
                var v = 0;
                sb.AppendLine($"\r\n\t M{m} P{p} {GetJson(measure)}");
                foreach (var voice in measure.Voices)
                {
                    var b = 0;
                    sb.AppendLine($"\r\n\t\t V{v} M{m} P{p} {GetJson(voice)}");
                    foreach (var beat in voice.Beats)
                    {

                        var n = 0;
                        sb.AppendLine($"\t\t\t B{b} V{v} M{m} P{p} {GetJson(beat)}");
                        foreach (var note in beat.Notes)
                        {

                            sb.AppendLine($"\t\t\t\t N{n} B{b} V{v} M{m} P{p} {GetJson(note)}");
                            n++;
                        }

                        b++;
                    }

                    v++;
                }

                var measureEvents = partEvents
                    .SkipWhile(e => e.MeasureIndex < m)
                    .TakeWhile(e => e.MeasureIndex == m)
                    .ToList();

                foreach (var timedEvent in measureEvents)
                {
                    sb.AppendLine($"\t\t {GetMidiEventString(timedEvent)}");
                }


                foreach (var group in measureEvents.GroupBy(e => e.On.Channel))
                {
                    sb.AppendLine($"\t\t Channel {group.Key}:");
                    foreach (var note in group)
                    {
                        sb.AppendLine($"\t\t\t {GetMidiEventString(note)}");
                    }
                }


                m++;
            }

            p++;
        }

        Save(sb.ToString(), "MidiMeasured", record, overwrite);
    }

    private static void WriteRawMidiData(MidiFile midi, RecordModel record, bool overwrite)
    {
        if (!overwrite && File.Exists(GetFileName("MidiRaw", record))) return;

        var sb = new StringBuilder();

        var parts = midi.Chunks.OfType<TrackChunk>().ToList();

        for (var p = 0; p < parts.Count; p++)
        {
            var part = parts[p];
            var partEvents = parts[p].GetTimedEvents().ToList();
            sb.AppendLine($"\r\n\t P{p}");


            for (var i = 0; i < partEvents.Count; i++)
            {
                var partEvent = partEvents[i];
                if (partEvent.Event is NoteEvent noteEvent)
                {
                    sb.AppendLine($"\t\t\t {i} {noteEvent.EventType.ToString()[4..],3} CH {noteEvent.Channel} NN {noteEvent.NoteNumber} V {noteEvent.Velocity} Time: {partEvent.Time}");
                }
                else if (partEvent.Event is PitchBendEvent pitch)
                {
                    sb.AppendLine($"\t\t\t {i} {"Pitch",3} CH {pitch.Channel} P {pitch.PitchValue} Time: {partEvent.Time}");
                }
                else if (partEvent.Event is MarkerEvent marker)
                {
                    sb.AppendLine($"\r\n\t\t P{p} {marker.Text} Time: {partEvent.Time}");
                }
            }
        }

        Save(sb.ToString(), "MidiRaw", record, overwrite);
    }

    private static string GetMidiEventString(TimedNoteEvent note)
    {
        var timing = $"Start: {note.Start.ToString(),6}; End: {note.End.ToString(),6}, Duration: {note.Duration.ToString(),5}";
        return $"{note.EventIndex.ToString(),5} On {note.On.NoteNumber,2}; {timing}; Ch: {note.On.Channel}; Velocity: {note.On.Velocity}";
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