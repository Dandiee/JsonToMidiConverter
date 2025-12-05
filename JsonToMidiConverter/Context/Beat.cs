using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Context;
using Melanchall.DryWetMidi.Interaction;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} V{Voice.Index} M{Measure.Index} P{Part.Index}")]
public sealed partial class Beat
{
    [JsonIgnore] public int Index { get; private set; }
    [JsonIgnore] public Voice Voice { get; private set; }
    [JsonIgnore] public Measure Measure => Voice.Measure;
    [JsonIgnore] public Part Part => Measure.Part;
    [JsonIgnore] public Song Song => Part.Song;

    [JsonIgnore] public Time Start { get; private set; }
    [JsonIgnore] public Time End { get; private set; }
    [JsonIgnore] public Time Dur { get; private set; }

    [JsonIgnore] public bool IsAccord { get; private set; }
    [JsonIgnore] public Beat? Next { get; private set; }
    [JsonIgnore] public Beat? Previous { get; private set; }
    [JsonIgnore] public bool LastInMeasure { get; private set; }
    [JsonIgnore] public bool TripletOverriden { get; set; }
    public List<int> OriginalDuration { get; set; } = [];

    public void SetNavigation(Voice voice, int index)
    {
        Index = index;
        Voice = voice;
        LastInMeasure = Index == Measure.Voices[Voice.Index].Beats.Count - 1;



        

        if (Index > 0)
        {
            Previous = Voice.Beats[Index - 1];
        }
        else if (Measure.Previous?.Voices.Count > Voice.Index)
        {
            var prevBeat = Measure.Previous?.Voices[Voice.Index].Beats;
            if (prevBeat != null)
            {
                Previous = prevBeat[^1];
            }
        }

        if (Previous != null)
        {
            Previous.Next = this;
        }


        if (!Part.IsPianoLike) // for piano we dont change the fuckin note order
        {
            Notes = Notes.OrderByDescending(e => e.StringNumber).ToList();
        }

        for (var i = 0; i < Notes.Count; i++)
        {
            Notes[i].SetNavigation(this, i);
        }
    }

    public void Build()
    {
        //OriginalDuration = Duration.Select(e => e).ToList();
        IsAccord = Notes.Count > 1;
        Notes.ForEach(e => e.Build());
    }

    public void SetTimes()
    {
        Start = Previous?.End ?? new Time();

        if ((Previous != null && Previous.Voice.Index != Voice.Index) ||
            (Next != null && Next.Voice.Index != Voice.Index))
        {

        }

        if (Index == 0)
        {
            var referenceStart =  Measure.StartTime;
            if (Start != referenceStart)
            {

            }
        }

        if (Voice.Index > 0 && Previous == null)
        {
            Start = Measure.StartTime;
        }

        Dur = GetDuration();
        if (Previous != null && Previous.GraceNote == "onBeat")
        {
            Dur -= Previous.GetDuration();
        }

        if (Next != null && Next.GraceNote == "beforeBeat")
        {
            var q = Next.GetDuration();
            Dur -= q;
        }
        End = Start + Dur;
    }

    public IEnumerable<Beat> Forward()
    {
        var current = this;
        while (current != null)
        {
            yield return current;
            current = current.Next;
        }
    }

    public IEnumerable<Beat> Backward()
    {
        var current = this;
        while (current != null)
        {
            yield return current;
            current = current.Previous;
        }
    }

    public Time GetDuration()
    {
        if (Is("B4 V0 M97 P2", "king"))
        {

        }

        var dur = new Time(Duration[0], Duration[1]);

        if (GraceNote == "beforeBeat" && Notes.Any(e => e.Slides.Contains(Slide.Legato)) && dur <= new Time(1, 16L))
        {
            //return dur * (2/3.0);
        }

        //if (GraceNote == "beforeBeat" && Notes.Any(e => e.Slides.Contains(Slide.Legato)) && dur <= new Time(1, 16L))
        //{
        //    return dur * (2 / 3.0);
        //}

        return dur;
    } 

    public bool Is(string name, string? filter = null)
    {
        if (string.IsNullOrEmpty(name)) return false;

        var trimmed = name.Trim().ToUpperInvariant();
        var isMatching = trimmed[0] switch
        {
            'B' => $"{this}".Equals(trimmed),
            'V' => $"{Voice}".Equals(trimmed),
            'M' => $"{Measure}".Equals(trimmed),
            'P' => $"{Part}".Equals(trimmed),
            _ => false
        };

        return isMatching && (string.IsNullOrEmpty(filter) || Part.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString() => $"B{Index} {Voice}";
}