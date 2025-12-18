namespace Api.Models;

public sealed class RawVoice
{
    public List<RawBeat> Beats { get; set; } = [];

    public bool Rest { get; set; }
    public bool HasSameRhythm { get; set; }
}