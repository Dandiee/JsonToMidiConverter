using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("V{Index} M{Measure.Index} P{Part.Index}")]
public sealed partial class Voice
{
    [JsonIgnore] public Measure Measure { get; private set; }
    [JsonIgnore] public Part Part => Measure.Part;
    [JsonIgnore] public Song Song => Part.Song;
    [JsonIgnore] public int Index { get; private set; }
    [JsonIgnore] public List<List<Beat>> BeamGroups { get; private set; } = [];

    public void SetNavigation(Measure measure, int index)
    {
        Index = index;
        Measure = measure;

        for (var i = 0; i < Beats.Count; i++)
        {
            Beats[i].SetNavigation(this, i);
        }
    }

    public void Build()
    {
        List<Beat>? currentBeamGroup = null;

        foreach (var beat in Beats)
        {
            Debug.Assert(beat is { BeamStart: true, BeamStop: false } || 
                         beat is { BeamStart: false, BeamStop: true } || 
                         beat is { BeamStart: false, BeamStop: false });

            beat.Build();
            if (beat.BeamStart)
            {
                currentBeamGroup = [beat];
                beat.BeamGroup = currentBeamGroup;

            }
            else if (beat.BeamStop)
            {
                currentBeamGroup.Add(beat);
                BeamGroups.Add(currentBeamGroup);
                beat.BeamGroup = currentBeamGroup;
                currentBeamGroup = null;
            }
            else if (currentBeamGroup != null)
            {
                currentBeamGroup.Add(beat);
                beat.BeamGroup = currentBeamGroup;
            }
        }
    } 

    public override string ToString() => $"V{Index} {Measure}";

    public bool Is(string name, string? filter = null)
    {

        if (string.IsNullOrEmpty(name)) return false;

        var trimmed = name.Trim().ToUpperInvariant();
        var isMatching = trimmed[0] switch
        {
            'V' => $"{this}".Equals(trimmed),
            'M' => $"{Measure}".Equals(trimmed),
            'P' => $"{Part}".Equals(trimmed),
            _ => false
        };

        return isMatching && (string.IsNullOrEmpty(filter) || Part.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

}