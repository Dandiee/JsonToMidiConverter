using JsonToMidiConverter.Context;
using JsonToMidiConverter.Models.Song;
using Melanchall.DryWetMidi.Core;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace JsonToMidiConverter.Test;

public record Event(MidiEvent MidiEvent, long Time);

public static class Dumper
{
    public static readonly IReadOnlyDictionary<MidiEventType, string> EventTypeNames =
        new Dictionary<MidiEventType, string>
        {
            [MidiEventType.NoteOn] = "On",
            [MidiEventType.NoteOff] = "Off",
            [MidiEventType.PitchBend] = "Pitch",
            [MidiEventType.Marker] = "Marker",
            [MidiEventType.ProgramChange] = "Program",
            [MidiEventType.ControlChange] = "Control",
        };

    public static readonly HashSet<string> KnownFuckedUpMeasures = new[] { "036", "136", "236", "336" }.ToHashSet();

    public static readonly IReadOnlyDictionary<Type, JsonSerializerOptions> JsonOptions =
        new Dictionary<Type, JsonSerializerOptions>
        {
            [typeof(Measure)] = GetTypeExcludedJsonOptions(nameof(Measure.Voices)),
            [typeof(Beat)] = GetTypeExcludedJsonOptions(nameof(Beat.Notes), nameof(Beat.Text)),
            [typeof(Part)] =
                GetTypeExcludedJsonOptions(nameof(Part.NewLyrics), nameof(Part.Measures), nameof(Part.Automations)),

        };

    public static readonly HashSet<string> ExcludedProperties = new[]
    {
        "Channel", "DeltaTime", "EventType", "NoteNumber"
    }.ToHashSet();

    public static readonly HashSet<MidiEventType> StrumSeparatorEvents = new[]
    {
        MidiEventType.NoteOff, MidiEventType.ControlChange, MidiEventType.PitchBend
    }.ToHashSet();

    public static bool IsMissing(Nóta note)
    {
        if (note.Part.InstrumentId == 1024 && note.Fret == 36 && note.Ghost) return true;

        return false;
    }

    public static void Dump(Song song, string midiPath, string? artist)
    {
        var midi = MidiFile.Read(midiPath);
        var output = ProcessMidi(song, midi);

        File.WriteAllText($"Logs_{artist}", output);
    }

    private static IEnumerable<Event> GetMidiEvents(TrackChunk chunk)
    {
        var absoluteTime = 0L;

        foreach (var midiEvent in chunk.Events)
        {
            absoluteTime += midiEvent.DeltaTime;
            //var time = ((absoluteTime + 9) / 10) * 10;
            var time = absoluteTime;
            yield return new(midiEvent, time);
        }
    }

    public static long R(this long n) => (long)Math.Round(n / 10d) * 10;

    public static void CollectSlide(Song song, string midiPath, string? artist)
    {
        var midi = MidiFile.Read(midiPath);
        var sb = new StringBuilder();
        SetSongMidiEvents(song, midi);

        var slides = song.Parts
            .SelectMany(e => e.Measures)
            .SelectMany(e => e.Beats)
            .SelectMany(e => e.Notes)
            .Where(e => e.Slide != Slide.None && !e.Tie);

        var eventsByChunks = midi.Chunks.OfType<TrackChunk>().Select(e => GetMidiEvents(e).ToList()).ToList();

        foreach (var slide in slides)
        {
            var attackNote = slide;
            var targetNote = attackNote.GetSlideTarget();
            var events = eventsByChunks[slide.Part.Index];
            var distance = targetNote.NoteNumber - attackNote.NoteNumber;


            var firstEvent = events[attackNote.MidiEventIndex.Value];
            var lastEvent = events[targetNote.MidiEventIndex.Value + 2];



            

            var slideEvents = GetSlideEvents(attackNote, targetNote, events).ToList();
            var holdTime = slideEvents[0].Off.Time - slideEvents[0].On.Time;
            var slideTime = slideEvents.Count > 2
                ? slideEvents[^2].Off.Time - slideEvents[1].On.Time
                : 0;

            var totalTime = slideEvents[^1].On.Time - slideEvents[0].On.Time;

            sb.AppendLine($"{artist}, Part{attackNote.Part.Index}, Measure{attackNote.Measure.Index}, Beat{attackNote.Beat.Index}, Note{attackNote.Index} ");
            sb.AppendLine($"Semitone distance: {distance} (NN{attackNote.NoteNumber} -> NN{targetNote.NoteNumber})");
            sb.AppendLine($"Beat Input: {GetJson(attackNote.Beat)}");
            sb.AppendLine($"Note Input: {GetJson(attackNote)} ");
            sb.AppendLine($"Total time: {totalTime.ToString().PadLeft(5)} ({firstEvent.Time} -> {lastEvent.Time})");
            sb.AppendLine($"Hold duration: {holdTime} ({(double)holdTime / totalTime:P1})");
            sb.AppendLine($"Slide duration: {slideTime} ({(double)slideTime/ totalTime:P1})");
            sb.AppendLine($"Played notes:");

            var isSliding = false;
            foreach (var slideEvent in slideEvents)
            {
                var duration = slideEvent.Off.Time - slideEvent.On.Time;

                if (slideEvent == slideEvents[0])
                {
                    sb.AppendLine($"\t Attack note");
                }
                else if (slideEvent != slideEvents[^1])
                {
                    if (!isSliding)
                    {
                        sb.AppendLine($"\t Slide notes");
                        isSliding = true;
                    }
                }
                else
                {
                    sb.AppendLine($"\t Landing note");
                    
                }

                sb.AppendLine($"\t - [{slideEvent.On.Time}] NN {(slideEvent.On.MidiEvent as NoteOnEvent).NoteNumber}; Duration = {duration.ToString().PadLeft(5)} ({slideEvent.On.Time} -> {slideEvent.Off.Time})");
            }
            for (var i = attackNote.MidiEventIndex.Value; i < targetNote.MidiEventIndex.Value + 2; i++)
            {

                var ev = events[i];
                if (ev.MidiEvent is NoteOnEvent on && attackNote.Channel == on.Channel)
                {
                    var offIndex = i;
                    while (!(events[offIndex].MidiEvent is NoteOffEvent e && e.NoteNumber == on.NoteNumber))
                    {
                        offIndex++;
                    }
                    var off = events[offIndex];


                }
            }

            sb.AppendLine("\r\n");
        }

        File.WriteAllText($"Slide_{artist}.data", sb.ToString());
    }

    private static IEnumerable<(Event On, Event Off, long Dur)> GetSlideEvents(Nóta attackNote, Nóta targetNote, List<Event> events)
    {
        for (var i = attackNote.MidiEventIndex.Value; i < targetNote.MidiEventIndex.Value + 2; i++)
        {
            var ev = events[i];
            if (ev.MidiEvent is NoteOnEvent on && attackNote.Channel == on.Channel)
            {
                var offIndex = i;
                while (!(events[offIndex].MidiEvent is NoteOffEvent e && e.NoteNumber == on.NoteNumber))
                {
                    offIndex++;
                }

                var duration = (events[offIndex].Time - ev.Time).R();

                yield return new(ev, events[offIndex], duration);
            }
        }
    }

    public static string ProcessMidi(Song song, MidiFile midi)
    {
        var sb = new StringBuilder();
        SetSongMidiEvents(song, midi);
        var chunks = midi.Chunks.OfType<TrackChunk>().ToList();

        Debug.Assert(song.Parts.Length == chunks.Count);


        foreach (var part in song.Parts)
        {
            var events = GetMidiEvents(chunks[part.Index]).ToList();

            var instrumentId = ((ProgramChangeEvent)events[0].MidiEvent).ProgramNumber;
            var trackName = ((SequenceTrackNameEvent)events.Single(e => e.MidiEvent.EventType == MidiEventType.SequenceTrackName).MidiEvent).Text;
            var instrumentName = ((InstrumentNameEvent)events.Single(e => e.MidiEvent.EventType == MidiEventType.InstrumentName).MidiEvent).Text;


            Debug.Assert(part.Instrument == instrumentName);
            Debug.Assert(part.Name == trackName);
            //Debug.Assert(part.InstrumentId == instrumentId);
            Debug.Assert(part.Measures.Length == events.Count(e => e.MidiEvent.EventType == MidiEventType.Marker) - 1);

            var partTitle =
                $"================================== P{part.Index} - I{part.InstrumentId}: {part.Instrument} {part.Name} ==================================";
            var separator = new string(Enumerable.Range(0, partTitle.Length).Select(_ => '-').ToArray());
            sb.AppendLine(separator);
            sb.AppendLine(partTitle);
            sb.AppendLine(separator);
            foreach (var measure in part.Measures)
            {
                if (measure.Rest) continue;

                sb.AppendLine($"\r\n\r\nM{measure.Index} P{part.Index}, Input = {GetJson(measure)}");
                foreach (var beat in measure.Beats)
                {
                    if (beat.Rest) continue;

                    sb.AppendLine(
                        $"\r\n\tB{beat.Index} M{measure.Index} P{part.Index}, Attr = [{GetAttributes(beat)}], Input = {GetJson(beat)}");
                    foreach (var note in beat.Notes)
                    {

                        var slideMarker = note.Slide != Slide.None ? $" Slide = {note.Slide} " : "";
                        var tieMarker = note.Tie ? " Tie " : "";

                        sb.AppendLine(
                            $"\t\tN{note.Index} B{beat.Index} M{measure.Index} P{part.Index} S{note.StringNumber} F{note.Fret}" +
                            $"{slideMarker}{tieMarker} Attr = [{GetAttributes(beat)}] Input = {GetJson(note)}");


                        if (note.MidiEventIndex.HasValue)
                        {
                            var from = note.MidiEventIndex.Value;
                            var to = note.MidiEventCount.HasValue
                                ? note.MidiEventCount.Value + from
                                : events.Count;

                            var pitchCounter = 0;

                            for (var i = from; i < to; i++)
                            {
                                var timedEvent = events[i];
                                EventTypeNames.TryGetValue(timedEvent.MidiEvent.EventType, out var niceName);

                                var ch = (timedEvent.MidiEvent as ChannelEvent)?.Channel.ToString() ?? string.Empty;
                                var nn = (timedEvent.MidiEvent as NoteEvent)?.NoteNumber.ToString() ?? string.Empty;

                                var properties = timedEvent.MidiEvent
                                    .GetType()
                                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(e => !ExcludedProperties.Contains(e.Name));

                                var attributes = string.Join("; ", properties
                                    .OrderBy(e => e.Name)
                                    .Select(prop => $"{prop.Name}: {prop.GetValue(timedEvent.MidiEvent)}"));

                                if (timedEvent.MidiEvent is PitchBendEvent)
                                {
                                    pitchCounter++;
                                }
                                else pitchCounter = 0;

                                if (pitchCounter == 3)
                                {
                                    sb.AppendLine($"\t\t\t..... More pitch bending");
                                }
                                else if (pitchCounter < 3)
                                {
                                    sb.AppendLine(
                                        $"\t\t\t{i.ToString().PadLeft(5)} {(niceName ?? timedEvent.MidiEvent.EventType.ToString()).PadRight(10)} " +
                                        $"Note: {nn.PadLeft(2)}; At: {timedEvent.Time}; Ch: {ch}; " +
                                        $"Delta: {timedEvent.MidiEvent.DeltaTime.ToString().PadLeft(6)} {attributes}");
                                }
                            }

                        }
                    }


                }
            }
        }

        return sb.ToString();
    }

    public static void SetSongMidiEvents(Song song, MidiFile midi)
    {
        var chunks = midi.Chunks.OfType<TrackChunk>().ToList();

        // pass 1: assign events to beats/notes
        foreach (var part in song.Parts)
        {
            var chunk = chunks[part.Index];
            var events = chunk.Events.ToList();

            Beat? lastMarkedBeat = null;
            var cursor = 0;

            foreach (var measure in part.Measures)
            {
                foreach (var beat in measure.Beats)
                {
                    if (beat.Rest || beat.Notes.All(e => e.Rest || e.Tie)) continue;

                    _ = GetBeatEventCursor(events, ref cursor, beat);
                }
            }
        }


        // pass 2: calculate event counts for notes
        foreach (var part in song.Parts)
        {
            Nóta prevNote = null;
            foreach (var measure in part.Measures)
            {
                foreach (var beat in measure.Beats)
                {
                    foreach (var note in beat.Notes)
                    {
                        if (note.MidiEventIndex.HasValue)
                        {
                            if (prevNote != null)
                            {
                                var distance = note.MidiEventIndex.Value - prevNote.MidiEventIndex.Value;
                                prevNote.MidiEventCount = distance;
                            }

                            prevNote = note;
                        }
                    }
                }
            }
        }
    }

    public static string GetAttributes(object model)
    {
        return model switch
        {
            Nóta note => string.Join(", ", note.Rest ? "Rest" : "", note.Tie ? "Tie" : ""),
            Beat beat => string.Join(", ", beat.Rest ? "Rest" : ""),

            _ => string.Empty
        };
    }

    public static string GetJson<T>(T model)
    {
        if (!JsonOptions.TryGetValue(typeof(T), out var options))
            options = new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
            };

        return JsonSerializer.Serialize(model, options);
    }

    private static bool IsPitchClearing(List<MidiEvent> events, int cursor, Nóta note)
    {
        var midiEvent = events[cursor];
        if (midiEvent is not PitchBendEvent pitch) return false;
        if (pitch.PitchValue != 8192) return false;
        if (pitch.Channel != note.Channel) return false;

        var q = note.GetNoteChannel();

        return true;
    }

    private static bool IsMatchingNote(List<MidiEvent> events, int cursor, Nóta note)
    {
        var midiEvent = events[cursor];
        if (midiEvent is not NoteOnEvent on) return false;
        if (on.DeltaTime != 0 || on.NoteNumber != note.NoteNumber) return false;

        var w = note.GetNoteNumber();

        return true;
    }

    public static int? GetBeatEventCursor(List<MidiEvent> events, ref int cursor, Beat beat)
    {
        //if (beat.Part.Index == 1)
        if (beat.Is("0485"))
        {

        }

        var originalCursor = cursor;
        int newCursor;

        do
        {
            cursor++;

            if (cursor > events.Count - 1)
            {
                if (!KnownFuckedUpMeasures.Contains(beat.Nameplate))
                {
                    Debugger.Break();
                }
                else
                {
                    cursor = originalCursor;
                    return null;
                }
            }

        } while (!IsMatchingBeat(events, cursor, beat, out newCursor));

        var first = beat.Notes.First(a => a.MidiEventIndex.HasValue).MidiEventIndex.Value;
        var last = beat.Notes.Last(a => a.MidiEventIndex.HasValue).MidiEventIndex.Value;

        if (events[cursor].EventType != MidiEventType.NoteOn)
        {

        }

        if (!IsMatchingMeasure(events, cursor, beat, out var m1))
        {
            Debugger.Break();
        }

        var result = cursor;
        cursor = newCursor;

        return result;
    }

    public static bool IsMatchingMeasure(List<MidiEvent> events, int cursor, Beat beat, out int measureIndex)
    {
        var measureCursor = cursor;
        for (var i = measureCursor; ; i--)
        {
            var ev = events[i];
            if (ev is MarkerEvent marker)
            {
                measureIndex = int.Parse(string.Join("", marker.Text.Where(char.IsDigit)));
                return measureIndex == beat.Measure.Index;
            }
        }
    }



    public static bool IsMatchingBeat(List<MidiEvent> events, int cursor, Beat beat, out int newCursor)
    {
        newCursor = cursor;

        var playedNotes = beat.Notes.Where(e => !e.Tie && !IsMissing(e)).ToList();
        if (playedNotes.Count == 1)
        {
            var note = playedNotes.Single();

            if (!IsMatchingNote(events, cursor, note)) return false;
            if (!IsPitchClearing(events, cursor - 1, note)) return false;

            note.MidiEventIndex = cursor;

            newCursor = cursor + 1;
        }
        else
        {
            if (beat.Part.IsPianoLike)
            {
                for (var i = 0; i < playedNotes.Count; i++)
                {
                    var noteOnIndex = cursor + i;
                    var pitchClearIndex = noteOnIndex - playedNotes.Count;

                    if (!IsMatchingNote(events, noteOnIndex, playedNotes[i])) return false;
                    if (!IsPitchClearing(events, pitchClearIndex, playedNotes[i])) return false;

                    playedNotes[i].MidiEventIndex = noteOnIndex;
                }

                newCursor = cursor + 2 * beat.Notes.Length - 1;
            }
            else
            {
                var totalSeparators = 0;


                for (var i = 0; i < playedNotes.Count; i++)
                {
                    var note = playedNotes[i];
                    if (note.Tie)
                        continue; // TODO: this gonna break at some point. the assumption is that the tiw notes are always at the end of the note list

                    var noteOnIndex = cursor + i * 2 + totalSeparators;
                    var pitchClearIndex = noteOnIndex - 1;

                    totalSeparators += GetFillerCount(events, pitchClearIndex, MidiEventType.NoteOff);

                    noteOnIndex = cursor + i * 2 + totalSeparators;
                    pitchClearIndex = noteOnIndex - 1;

                    if (!IsMatchingNote(events, noteOnIndex, note)) return false;
                    if (!IsPitchClearing(events, pitchClearIndex, note)) return false;

                    playedNotes[i].MidiEventIndex = noteOnIndex;

                    if (note.Bend != null || note.Vibrato)
                    {
                        var fillerEvents = GetFillerCount(events, noteOnIndex + 1, MidiEventType.ControlChange, MidiEventType.PitchBend);
                        totalSeparators += fillerEvents - 1;
                    }
                }

                newCursor = cursor + 2 * playedNotes.Count + totalSeparators - 1;
            }
        }

        return true;
    }

    public static int GetFillerCount(List<MidiEvent> events, int cursor, params MidiEventType[] types)
    {
        var i = cursor;
        while (types.Contains(events[i++].EventType)) ;
        return i - cursor - 1;
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

    public static void DumpJsonInputs(int songId, string? midiFilePath = null)
    {
        var originalTimeMap = Time.Map;

        var meta = Database.GetMetaData(songId);
        var song = Database.GetMidiData(songId);

        var opts = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        File.WriteAllText($"MetaData_{meta.Artist}.json", JsonSerializer.Serialize(meta, opts));
        File.WriteAllText($"MidiData_{meta.Artist}.json", JsonSerializer.Serialize(song, opts));


        var dumpMid = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(15360) };
        Time.Map = song.Parts[0].GetTempo(dumpMid);
        song.Build();
        Time.Map = originalTimeMap;

        var sb = new StringBuilder();
        foreach (var part in song.Parts)
        {
            sb.AppendLine($"\r\n\r\nP{part.Index} {GetJson(part)}");
            foreach (var measure in part.Measures)
            {
                sb.AppendLine($"\r\n\tM{measure.Index} P{part.Index}: {GetJson(measure)}");
                foreach (var beat in measure.Beats)
                {
                    sb.AppendLine($"\t\tB{beat.Index} M{measure.Index} P{part.Index}: {GetJson(beat)}");
                    foreach (var note in beat.Notes)
                    {
                        sb.AppendLine(
                            $"\t\t\tN{note.Index} B{beat.Index} M{measure.Index} P{part.Index}: {GetJson(note)}");
                    }
                }
            }
        }

        File.WriteAllText($"MidiFormatted_{meta.Artist}.json", sb.ToString());


        sb = new StringBuilder();
        if (midiFilePath != null)
        {
            var midi = MidiFile.Read(midiFilePath);


            var partIndex = 0;
            foreach (var part in midi.Chunks.OfType<TrackChunk>())
            {
                var eventIndex = 0;
                var time = 0L;
                var measureIndex = 0;

                sb.AppendLine($"P{partIndex}");

                foreach (var midiEvent in part.Events)
                {
                    time += midiEvent.DeltaTime;

                    EventTypeNames.TryGetValue(midiEvent.EventType, out var niceName);

                    var ch = (midiEvent as ChannelEvent)?.Channel.ToString() ?? string.Empty;
                    var nn = (midiEvent as NoteEvent)?.NoteNumber.ToString() ?? string.Empty;

                    var properties = midiEvent
                        .GetType()
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(e => !ExcludedProperties.Contains(e.Name));

                    var attributes = string.Join("; ", properties
                        .OrderBy(e => e.Name)
                        .Select(prop => $"{prop.Name}: {prop.GetValue(midiEvent)}"));

                    if (midiEvent.EventType == MidiEventType.Marker)
                    {
                        sb.AppendLine($"\t M{measureIndex} P{partIndex} -------------------------------------------------------------------------------------------------------------------- ");
                        measureIndex++;
                    }

                    sb.AppendLine(
                        $"\t\t{eventIndex++.ToString().PadLeft(5)} {(niceName ?? midiEvent.EventType.ToString()).PadRight(10)} " +
                        $"Note: {nn.PadLeft(2)}; At: {time}; Ch: {ch}; " +
                        $"Delta: {midiEvent.DeltaTime.ToString().PadLeft(6)} {attributes}");
                }

                partIndex++;
            }


        }

        File.WriteAllText($"MidiRaw_{meta.Artist}.dani", sb.ToString());

    }
}