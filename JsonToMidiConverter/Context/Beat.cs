using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Context;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;

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
    public List<long> OriginalDuration { get; set; } = [];

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

        if (Is("P0", "money"))
        {
            if (Index == 0 && Start != Measure.StartTime)
            {

            }
        }

        if (Voice.Index > 0 && Previous == null)
        {
            Start = Measure.StartTime;
        }

        if (!Part.Anacrusis && Index == 0 && Math.Abs(Start.Tick - Measure.StartTime.Tick) > 10 && GraceNote == null && !Rest)
        {

        }

        Dur = GetDuration();
        //if (GraceNote == null)
        //{
        //    if (Previous != null && Previous.GraceNote == "onBeat")
        //    {
        //        var graceCluster = Backward().Skip(1).TakeWhile(e => e.GraceNote == "onBeat").ToList();
        //        var graceClusterDuration = graceCluster.Sum(e => e.GetDuration().Tick);
        //
        //        if (false && Dur.Tick <= graceClusterDuration)
        //        {
        //            Dur -= graceClusterDuration / 2;
        //        }
        //        else Dur -= graceClusterDuration;
        //    }
        //
        //    if (Next != null && Next.GraceNote == "beforeBeat")
        //    {
        //        var graceCluster = Forward().Skip(1).TakeWhile(e => e.GraceNote == "beforeBeat").ToList();
        //        var graceClusterDuration = graceCluster.Sum(e => e.GetDuration().Tick);
        //
        //        if (false && Dur.Tick <= graceClusterDuration)
        //        {
        //            Dur -= graceClusterDuration / 2;
        //        }
        //        else Dur -= graceClusterDuration;
        //
        //    }
        //}
       //else if (GraceNote == "onBeat")
       //{
       //    // TODO does not support clusters
       //    if (Dur >= Next.GetDuration())
       //    {
       //
       //    }
       //}
       //else if (GraceNote == "beforeBeat")
       //{
       //    // TODO does not support clusters
       //}
       //else throw new Exception();

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

        var dur = new Time(Duration[0], Duration[1]);
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