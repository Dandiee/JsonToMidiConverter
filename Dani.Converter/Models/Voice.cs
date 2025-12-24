using System.Diagnostics;
using Dani.Data.Models.Enums;

using DataVoice = Dani.Data.Models.Parts.Voice;

namespace Dani.Converter.Models;

[DebuggerDisplay("V{Index} M{Measure.Index} P{Part.Index}")]
public sealed class Voice : MusicalElement<Voice, Measure>
{
    public Measure Measure => Parent;
    public List<Beat> Beats { get; }

    public List<List<Beat>> BeamGroups { get; } = [];
    

    public Voice(Measure measure, DataVoice data, int index) 
        : base(measure.Part, measure, index)
    {
        Beats = new List<Beat>(data.Beats.Count);
        for (var i = 0; i < data.Beats.Count; i++)
        {
            Beats.Add(new Beat(this, data.Beats[i], i));
        }
    }

    protected override Voice? GetPrevious(object? state = null) => null;

    public void Build()
    {
        List<Beat>? currentBeamGroup = null;

        foreach (var beat in Beats)
        {
            if (beat.BeamSpan == Spanner.Start)
            {
                currentBeamGroup = [beat];
                beat.BeamGroup = currentBeamGroup;

            }
            else if (beat.BeamSpan == Spanner.Stop)
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
            'P' => $"{Measure.Part}".Equals(trimmed),
            _ => false
        };

        return isMatching && (string.IsNullOrEmpty(filter) || Measure.Part.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

}