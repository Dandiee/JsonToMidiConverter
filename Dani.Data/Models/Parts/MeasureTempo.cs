using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class MeasureTempo : Serializable
{
    public int Type { get; set; }
    public int Bpm { get; set; }
    public int Progressive { get; set; }
}