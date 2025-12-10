using System.Diagnostics;
using System.Text.Json.Serialization;
using JsonToMidiConverter.Context;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("M{Index} P{Part.Index}")]
public sealed partial class Measure : MusicalElement<Measure>
{
    [JsonIgnore]public Song Song => Part.Song;
    [JsonIgnore] public int OriginalIndex { get; set; }
    [JsonIgnore] public Time Signature { get; set; }
    [JsonIgnore] public int RepeatIndex { get; set; }

    public void SetNavigation(Part part, int index)
    {
        Index = index;
        Part = part;

        if (Index > 0)
        {
            Previous = Part.Measures[Index - 1];
            Previous.Next = this;
        }

        for (var i = 0; i < Voices.Count; i++)
        {
            Voices[i].SetNavigation(this, i);
        }
    }

    public void Build()
    {
        Start = Previous?.End ?? new Time();
        Duration = Part.Anacrusis && Index == 0 
            ? new Time(Voices[0].Beats.Where(e => string.IsNullOrEmpty(e.GraceNote)).Sum(e => e.Duration.Tick))
            : Signature;
        End = Start + Duration;

        Voices.ForEach(v => v.Build());
    }


    public override string ToString() => $"M{Index} {Part}";
}