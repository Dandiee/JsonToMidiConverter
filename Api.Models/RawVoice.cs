using Api.Generators;

namespace Api.Models;


[AutoSerialize]
public sealed partial class RawVoice : Serializable
{
    public List<RawBeat> Beats { get; set; } = [];

    public bool Rest { get; set; }
    public bool HasSameRhythm { get; set; }
}