using System.Collections;
using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Context;
using Melanchall.DryWetMidi.Common;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("N{Index} B{Beat.Index} V{Voice.Index} M{Measure.Index} P{Part.Index}")]
public sealed partial class Nota
{
    [JsonIgnore] public int Index { get; private set; }
    [JsonIgnore] public Beat Beat { get; private set; }
    [JsonIgnore] public Voice Voice => Beat.Voice;
    [JsonIgnore] public Measure Measure => Voice.Measure;
    [JsonIgnore] public Part Part => Measure.Part;
    [JsonIgnore] public Song Song => Part.Song;
    [JsonIgnore] public int Channel { get; private set; }
    [JsonIgnore] public SevenBitNumber NoteNumber { get; set; }
    [JsonIgnore] public bool WillBeTied { get; private set; }
    [JsonIgnore] public List<Context.Slide> Slides { get; private set; } = [];
    [JsonIgnore] public List<TimedNoteEvent> MidiNoteEvents { get; set; } = [];
    [JsonIgnore] public TieContext? TieDetails { get; private set; }
    [JsonIgnore] public Nota? Next { get; private set; }
    [JsonIgnore] public Nota? Previous { get; private set; }
    [JsonIgnore] public bool LastInBeat { get; private set; }
    [JsonIgnore] public Time? TremoloDuration { get; private set; }
    [JsonIgnore] public int PureNoteNumber { get; private set; }
    [JsonIgnore] public Time Starts { get; private set; }
    [JsonIgnore] public Time Dur { get; private set; }
    [JsonIgnore] public Time Ends { get; private set; }

    public void SetNavigation(Beat beat, int index)
    {
        Index = index;
        Beat = beat;
        LastInBeat = Index == Beat.Notes.Count - 1;

        var prevBeat = Beat.Previous;
        while (prevBeat != null && Previous == null)
        {
            var prevNote = prevBeat.Notes.FirstOrDefault(n => (int)n.StringNumber == (int)StringNumber);
            if (prevNote != null)
            {
                Previous = prevNote;
                prevNote.Next = this;
            }
            else prevBeat = prevBeat.Previous;
        }
    }

    public void Build()
    {
        Slides = SlideString?.ToSlides().ToList() ?? [];
        NoteNumber = (this.GetNoteNumber() + Part.Capo).To7();
        PureNoteNumber = this.GetNoteNumber(false) + Part.Capo;
        Channel = this.GetNoteChannel();

        if (Tremolo.Count > 0)
        {
            TremoloDuration = new Time(Tremolo[0], Tremolo[1]);
        }

        WillBeTied = Next?.Tie ?? false;

        if (Tie && !WillBeTied)
        {
            TieDetails = new TieContext(this);

            foreach (var tiedNote in TieDetails.FullChain)
            {
                tiedNote.TieDetails = TieDetails;
            }
        }
    }

    public void SetTimings()
    {
        Beat.SetTimes();
        Beat.Next?.SetTimes();

        Starts = Beat.Start;




        Ends = TieDetails?.Destination.Beat.End ?? Beat.End;

        TieDetails?.Destination.Beat.SetTimes();

        var strum = Part.IsPianoLike ? new Time() : new Time(100 * Index);

        if (Beat.LetRing)
        {
            var firstNonRinging = Beat.Forward().SkipWhile(beat => beat.LetRing && beat.Next != null).First();
            if (Ends < firstNonRinging.End)
            {
                Ends = firstNonRinging.End;
            }
        }

        if (TieDetails != null)
        {
            foreach (var note in TieDetails.FullChain)
            {
                if (note.Beat.LetRing)
                {
                    var firstNonRinging = note.Beat.Forward().SkipWhile(beat => beat.LetRing && beat.Next != null).First();
                    if (Ends < firstNonRinging.End)
                    {
                        Ends = firstNonRinging.End;
                    }
                }
            }
        }

        Ends += strum;
        if (Slides.Contains(Slide.Below))
        {
            Starts -= 1920; 
        }

        if (Slides.Contains(Slide.Above))
        {
            Starts -= 1920;
        }

        Dur = Ends - Starts;

        
    }



    public IEnumerable<int> GetEmittedNotes()
    {
        if (!Tie &&
            !TremoloDuration.HasValue &&
            !Slides.Contains(Context.Slide.Below) &&
            !Slides.Contains(Context.Slide.Above))
        {
            yield return NoteNumber;
        }

        foreach (var slide in Slides)
        {
            foreach (var note in GetSlideEmittedNotes(this, slide))
            {
                yield return note;
            }
        }

        if (TremoloDuration.HasValue)
        {
            var noteDuration = WillBeTied ? TieDetails.Destination.Ends.Tick - TieDetails.Source.Starts.Tick : Dur.Tick;
            var repeats = noteDuration / TremoloDuration.Value.Tick;
            for (var i = 0; i < repeats; i++)
            {
                if (Tie && i == 0) continue;
                if (Dead)
                {
                    if (!Tie && i == 0) yield return NoteNumber;

                    continue;
                }

                yield return NoteNumber;
            }
        }
    }

    private static IEnumerable<int> GetSlideEmittedNotes(Nota note, Context.Slide slide)
    {
        var target = GetSlideTargetPitch(slide, note);
        var delta = target - note.Fret;
        var sign = Math.Sign(delta);
        var steps = Math.Abs(target - note.Fret);

        if (steps > 10)
        {

        }

        if (slide == Context.Slide.Shift && delta == 0) yield break;

        if (slide == Context.Slide.Shift && delta == 0 && note.WillBeTied && note.TieDetails.Destination.Slides.Count == 0)
        {
            sign = -1;
            steps = 2;
        }

        if (slide == Context.Slide.Below && delta == 0)
        {
            sign = -1;
            steps = 1;
        }

        if (slide == Context.Slide.Below || slide == Context.Slide.Above)
        {
            for (var i = 1; i < steps; i++)
            {
                yield return note.PureNoteNumber + sign * (steps - i);
            }

            yield return note.NoteNumber;
        }
        else
        {
            for (var i = 1; i < steps; i++)
            {
                yield return note.PureNoteNumber + sign * i;
            }
        }
    }


    public static int GetSlideTargetPitch(Context.Slide slide, Nota note)
    {
        if (slide == Context.Slide.Shift || slide == Context.Slide.Legato)
        {
            var targetNote = note.Next;
            if (targetNote.NoteNumber == note.NoteNumber && targetNote.Tie)
            {
                targetNote = targetNote.Next;
            }
            return targetNote.Fret;
        }

        var affectedStrings = note.Beat.Notes
            .Where(e => (slide != Slide.Below && slide != Slide.Downwards) || e.Fret > 0)
            .ToList();

        if (affectedStrings.Count == 0) return note.Fret;

        var maxFretSeparation = affectedStrings.Max(e => e.Fret) - affectedStrings.Min(e => e.Fret);
        var moveTogether = maxFretSeparation < 10;

        if (slide == Context.Slide.Upwards || slide == Context.Slide.Above)
        {
            // the killin the name: https://www.songsterr.com/a/wsa/rage-against-the-machine-killing-in-the-name-tab-s360t5
            // really wants to go above 24, on ryhtm guitar at measure 46, there are two double-upwards, both goes up 10 steps,
            // including fret 15 on string 4, resulting in fret 25 - both strings go up equally 10 steps each-each
            // but enter sandmen: https://www.songsterr.com/a/wsa/metallica-enter-sandman-tab-s19
            // on the lead guitar at measure 99 after a long tie chain theres a double upwards from 17/16 
            // both note goes up 6 semitones, stopping at 24. if we go till 25 we overflow with one extra note on each note 
            var maxChordFret = note.Beat.Notes.Where(e => e.Slides.Contains(slide)).Max(e => e.Fret);
            var maxTargetFret = Math.Min(Math.Max(24, maxChordFret), maxChordFret + 10);
            var maxDistance = maxTargetFret - maxChordFret;

            if (slide == Context.Slide.Upwards && maxDistance == 9)
            {
                return note.Fret + 10; // the killin in the name rule or idk
            }

            return note.Fret + maxDistance;
        }
        if (slide == Context.Slide.Downwards || slide == Context.Slide.Below)
        {
            if (note.Fret == 0) return 0;

            var minChordFret = note.Beat.Notes
                .Where(e => e.Slides.Contains(slide))
                .Where(e => e.Fret != 0)
                .Min(e => e.Fret);

            var minTargetFret = minChordFret - Math.Min(10, minChordFret);
            var minDistance = minTargetFret - minChordFret;

            if (!moveTogether)
            {
                return Math.Max(0, note.Fret - Math.Min(10, note.Fret));
            }

            return Math.Max(0, note.Fret + minDistance);
        }

        throw new Exception("what slide");
    }


    public IEnumerable<Nota> Forward()
    {
        var current = this;
        while (current != null)
        {
            yield return current;
            current = current.Next;
        }
    }

    public override string ToString() => $"N{Index} {Beat}";
    


    public bool Is(string name, string? filter = null)
    {
        if (string.IsNullOrEmpty(name)) return false;

        var trimmed = name.Trim().ToUpperInvariant();
        var isMatching = trimmed[0] switch
        {
            'N' => $"{this}".Equals(trimmed),
            'B' => $"{Beat}".Equals(trimmed),
            'V' => $"{Voice}".Equals(trimmed),
            'M' => $"{Measure}".Equals(trimmed),
            'P' => $"{Part}".Equals(trimmed),
            _ => false
        };

        return isMatching && (string.IsNullOrEmpty(filter) || Part.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

}

public sealed class TieContext
{
    public Nota Source { get; }
    public Nota Destination { get; }
    public IReadOnlyList<Nota> FullChain { get; }
    //public Time FullDuration { get; }

    public TieContext(Nota destinationNote)
    {
        if (!destinationNote.Tie || destinationNote.WillBeTied) throw new Exception("no");

        var chain = new List<Nota> { destinationNote };

        while (chain[^1].Previous != null && (chain[^1].Tie))
        {
            chain.Add(chain[^1].Previous!);
        }

        chain.Reverse();

        FullChain = chain;
        Source = FullChain[0];
        Destination = FullChain[^1];
        //InBetweenNotes = FullChain.Skip(1).Take(FullChain.Count - 2).ToList();
        //FullDuration = new Time(FullChain.Sum(e => e.ActualDuration.Tick));
        //FullDuration = new Time(FullChain.Sum(e => e.Dur.Tick));
    }

    public Time GetFullDuration() => new Time(FullChain.Sum(e => e.Dur.Tick));
}