using Api.Generators;
using Api.Models.Serialization;

namespace Api.Models.Parts;

[AutoSerialize]
public partial class MeasureTempo : Serializable
{
    public int Type { get; set; }
    public int Bpm { get; set; }
    public int Progressive { get; set; }
}