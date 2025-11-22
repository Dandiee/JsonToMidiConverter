using System.Diagnostics;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("V{Index} M{Measure.Index} P{Part.Index}")]
public sealed partial class Voice
{
    [JsonIgnore] public Measure Measure { get; private set; }
    [JsonIgnore] public Part Part => Measure.Part;
    [JsonIgnore] public Song Song => Part.Song;
    [JsonIgnore] public int Index { get; private set; }

    public void Build(Measure measure, int index)
    {
        Index = index;
        Measure = measure;

        for (var i = 0; i < Beats.Length; i++)
        {
            Beats[i].Build(this, i);
        }
    }
}