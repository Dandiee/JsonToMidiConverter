using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Songs;

[AutoSerialize]
public partial class Track : Serializable
{
    public int InstrumentId { get; set; }
    public string Instrument { get; set; } = string.Empty;
    public int Views { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<sbyte> Tuning { get; set; } = [];
    public string Hash { get; set; } = string.Empty;
    public byte Difficulty { get; set; }
    public bool IsVocalTrack { get; set; }
}