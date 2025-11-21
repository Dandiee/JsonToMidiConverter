using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;

public class Song
{
    public int songId { get; set; }
    public int revisionId { get; set; }
    public Part[] parts { get; set; } = Array.Empty<Part>();

    public void Build()
    {
        parts = parts.OrderBy(e => e.partId).ToArray();

        for (var i = 0; i < parts.Length; i++)
        {
            parts[i].Build(this, i);
        }
    }
}

[DebuggerDisplay("P{Index}")]
public class Part
{
    public string name { get; set; } = string.Empty;
    public double balance { get; set; }
    public double volume { get; set; }
    public Measure[] measures { get; set; } = Array.Empty<Measure>();
    public int frets { get; set; }
    public int[] tuning { get; set; } = Array.Empty<int>();
    public int strings { get; set; }
    public int instrumentId { get; set; }
    public string instrument { get; set; } = string.Empty;
    public Newlyric[] newLyrics { get; set; } = Array.Empty<Newlyric>();
    public int partId { get; set; }
    public Automations automations { get; set; } = new();
    public int version { get; set; }
    public int songId { get; set; }
    public int revisionId { get; set; }

    public int Index { get; private set; }
    public Song Song { get; private set; }

    public void Build(Song song, int index)
    {
        Index = index;
        Song = song;

        for (var i = 0; i < measures.Length; i++)
        {
            measures[i].Build(this, i);
        }
    }
}

public class Automations
{
    public Tempó[] tempo { get; set; } = Array.Empty<Tempó>();
}

public class Tempó
{
    public int measure { get; set; }
    public double position { get; set; }
    public int bpm { get; set; }
    public int type { get; set; }
    public bool visible { get; set; }
}

[DebuggerDisplay("M{Index} P{Part.Index}")]
public class Measure
{
    public Voice[] voices { get; set; } = Array.Empty<Voice>();
    public int[] signature { get; set; } = Array.Empty<int>();
    [JsonIgnore]
    public byte numerator => (byte)signature[0];
    [JsonIgnore]
    public byte denominator => (byte)signature[1];
    public Marker? marker { get; set; }
    public bool rest { get; set; }

    public int Index { get; private set; }
    public Part Part { get; private set; }
    public Song Song => Part.Song;
    public Beat[] Beats => voices.Single().beats;

    public void Build(Part part, int index)
    {
        Index = index;
        Part = part;

        for (var i = 0; i < voices.Length; i++)
        {
            voices[i].Build(this, i);
        }
    }

    public Measure? GetNext()
    {
        if (Index >= Part.measures.Length - 1)
            return null;

        return Part.measures[Index + 1];
    }

    public Measure? GetPrevious()
    {
        if (Index <= 0)
            return null;

        return Part.measures[Index - 1];
    }

}

public class Marker
{
    public string text { get; set; } = string.Empty;
    public int width { get; set; }
}

[DebuggerDisplay("V{Index} M{Measure.Index} P{Part.Index}")]
public class Voice
{
    public Beat[] beats { get; set; } = Array.Empty<Beat>();

    public Measure Measure { get; private set; }
    public Part Part => Measure.Part;
    public Song Song => Part.Song;
    public int Index { get; private set; }

    public void Build(Measure measure, int index)
    {
        Index = index;
        Measure = measure;

        for (var i = 0; i < beats.Length; i++)
        {
            beats[i].Build(this, i);
        }
    }
}

[DebuggerDisplay("B{Index} M{Measure.Index} P{Part.Index}")]
public class Beat
{
    public Nóta[] notes { get; set; } = Array.Empty<Nóta>();
    public string velocity { get; set; } = string.Empty;

    /// <summary>
    /// THIS IS ONYL FOR VISUAL REPRESENTATION ON THE MUSIC SHEET DONT LET IT CONFUSE YOU AGAIN!
    /// </summary>
    public int type { get; set; }
    public bool palmMute { get; set; }
    public int[] duration { get; set; } = Array.Empty<int>();
    public byte numerator => (byte)duration[0];
    public byte denominator => (byte)duration[1];
    public bool beamStart { get; set; }
    public bool beamStop { get; set; }
    public bool vibrato { get; set; }
    public Text? text { get; set; }
    public bool letRing { get; set; }

    /// <summary>
    ///  THIS IS ALSO JUST HERE TO FUCK WITH ME, IGNORE THEFUCKER
    /// </summary>
    public int dots { get; set; }
    public bool rest { get; set; }
    public bool tapping { get; set; }
    public int tuplet { get; set; }
    public bool tupletStart { get; set; }
    public bool tupletStop { get; set; }
    public string? graceNote { get; set; }

    public int Index { get; private set; }
    public Voice Voice { get; private set; }
    public Measure Measure => Voice.Measure;
    public Part Part => Measure.Part;
    public Song Song => Part.Song;
    public MusicalTimeSpan MusicalDuration { get; private set; }

    public Nóta[] ReversedNotes { get; set; }

    public void Build(Voice voice, int index)
    {
        ReversedNotes = notes;
        notes = notes.Reverse().ToArray();

        Index = index;
        Voice = voice;
        MusicalDuration = new MusicalTimeSpan(duration[0], duration[1]);

        for (var i = 0; i < notes.Length; i++)
        {
            notes[i].Build(this, i);
        }
    }

    public Beat? GetNext()
    {
        if (Index < Measure.Beats.Length - 1)
            return Measure.Beats[Index + 1];

        var nextMeasure = Measure.GetNext();
        if (nextMeasure != null)
            return nextMeasure.Beats[0];

        return null;

    }

    public Beat? GetPrevious()
    {
        if (Index > 0)
            return Measure.Beats[Index - 1];

        var previousMeasure = Measure.GetPrevious();
        if (previousMeasure != null)
            return previousMeasure.Beats[^1];

        return null;
    }


    public long GetMeasureStartDuration(TempoMap tempoMap)
        => Measure.Beats
            .TakeWhile(e => e != this)
            .Sum(e => e.MusicalDuration.ToTicks(tempoMap));
}

public class Text
{
    public string text { get; set; } = string.Empty;
    public int width { get; set; }
}

[DebuggerDisplay("N{Index} B{Beat.Index} M{Measure.Index} P{Part.Index} STR{StringNumber}/FRT{fret} NN{NoteNumber}")]
public class Nóta
{
    public int fret { get; set; }
    [JsonPropertyName("string")]
    public double StringNumber { get; set; }
    public string? slide { get; set; }
    public bool vibrato { get; set; }
    public bool hp { get; set; }
    public bool tie { get; set; }
    public bool rest { get; set; }
    //public int[] tremolo { get; set; }
    public bool staccato { get; set; }
    public double accentuated { get; set; }
    public bool ghost { get; set; }
    public string? harmonic { get; set; }
    public double harmonicFret { get; set; }
    public Bend? bend { get; set; }
    public bool dead { get; set; }

    public int Index { get; private set; }
    public Beat Beat { get; private set; }
    public Voice Voice => Beat.Voice;
    public Measure Measure => Voice.Measure;
    public Part Part => Measure.Part;
    public Song Song => Part.Song;
    public TimedEvent NoteOnEvent { get; set; }

    public List<TimedEvent> Events { get; } = new();

    public int NoteNumber { get; set; }

    public Nóta GetNext()
    {
        var nextBeat = Beat.GetNext();

        return nextBeat.notes[0];

        return null;
    }

    public Nóta GetTie()
    {
        if (!tie) throw new Exception("The note is not tied.");

        var measure = Measure;

        while (true)
        {
            var beatStartIndex = measure == Measure
                ? Beat.Index - 1
                : measure.Beats.Length - 1;

            for (var b = beatStartIndex; b > -1; b--)
            {
                var beat = measure.Beats[b];
                foreach (var note in beat.notes)
                {
                    if (note.IsPitchEqual(this))
                    {
                        return note;
                    }
                }
            }

            measure = Part.measures[measure.Index - 1];
        }
    }

    public IEnumerable<Nóta> GetTies()
    {
        if (!tie) throw new Exception("The note is not tied.");

        var tieNote = this;

        while (true)
        {
            yield return tieNote;
            if (tieNote.tie)
            {
                tieNote = tieNote.GetTie();
            }
            else break;
        }
    }

    public Nóta GetSlideTarget()
    {
        if (string.IsNullOrEmpty(slide)) throw new Exception("The not is not a slide.");

        var nextBeat = Beat.GetNext();
        while (true)
        {
            var targetNote = nextBeat.notes.SingleOrDefault(e => e.StringNumber == StringNumber);
            if (targetNote != null)
            {
                return targetNote;
            }

            nextBeat = nextBeat.GetNext();
        }
    }

    public bool WillBeTied()
    {
        var nextBeat = Beat.GetNext();
        if (nextBeat == null) return false;

        return nextBeat.notes.Any(e => e.tie && e.IsPitchEqual(this));
    }

    public bool IsInInbetweenTie()
    {
        if (!tie) return false;

        for (var m = Measure.Index; m < Part.measures.Length; m++)
        {
            var measure = Part.measures[m];
            var beatStartIndex = measure == Measure
                ? Beat.Index + 1
                : 0;

            for (var b = beatStartIndex; b < measure.Beats.Length; b++)
            {
                var beat = measure.Beats[b];
                foreach (var note in beat.notes)
                {
                    if ((int)note.StringNumber == (int)StringNumber)
                    {
                        return note.tie && note.fret == fret;
                    }
                }
            }
        }

        return false;
    }

    public bool IsPitchEqual(Nóta note) => note.fret == fret && (int)note.StringNumber == (int)StringNumber;

    public void Build(Beat beat, int index)
    {
        Index = index;
        Beat = beat;
        NoteNumber = GetNoteNumber();
    }

    public void Is(int noteIndex, int beatIndex, int measureIndex, int? partIndex = null)
    {
        if (Index == noteIndex && Beat.Index == beatIndex && Measure.Index == measureIndex &&
            (!partIndex.HasValue || Part.Index == partIndex.Value)) Debugger.Break();
    }

    public int GetNoteNumber()
    {
        if (Part.instrumentId == 1024 || (int)StringNumber == -1)
        {
            return fret;
        }

        int openStringPitch = Part.tuning.Length == 0
            ? (int)StringNumber // Fallback
            : (int)Part.tuning[(int)StringNumber];

        if (harmonic == "natural")
        {
            switch (fret) // Or note.harmonicFret
            {
                case 12: return (openStringPitch + 12);
                case 7: return (openStringPitch + 19);
                case 5: return (openStringPitch + 24);
                case 4: return (openStringPitch + 28);
                case 9: return (openStringPitch + 28); // 9th fret harmonic is same as 4th
                case 3: return (openStringPitch + 31); // 3rd fret is +2 Octaves + 5th
            }
        }

        // 4. STANDARD FRETTED NOTE
        return (openStringPitch + fret);
    }
}

public class Bend
{
    public double tone { get; set; }
    public Point[] points { get; set; } = Array.Empty<Point>();
}

public class Point
{
    public int position { get; set; }
    public double tone { get; set; }
}

public class Newlyric
{
    public int line { get; set; }
    public int offset { get; set; }
    public string text { get; set; } = string.Empty;
}
