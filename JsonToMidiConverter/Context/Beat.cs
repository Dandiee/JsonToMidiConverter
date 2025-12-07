using System.Diagnostics;
using System.Text.Json.Serialization;

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

    [JsonIgnore] public bool IsAccord { get; private set; }
    [JsonIgnore] public Beat? Next { get; private set; }
    [JsonIgnore] public Beat? Previous { get; private set; }
    [JsonIgnore] public bool LastInMeasure { get; private set; }
    private Time? _duration;
    [JsonIgnore]
    public Time Duration
    {
        get
        {
            if (!_duration.HasValue)
            {
                _duration = new Time(DurationArray[0], DurationArray[1]);
            }

            return _duration.Value;
        }
        set => _duration = value;
    }

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
        IsAccord = Notes.Count > 1;
        Notes.ForEach(e => e.Build());
    }

    public void SetTimes()
    {
        Start = Previous?.End ?? new Time();

        if (Voice.Index > 0 && Previous == null)
        {
            Start = Measure.Start;
        }

        End = Start + Duration;
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