namespace JsonToMidiConverter.Models.Song;

public sealed partial class Song
{
    public void Build()
    {
        parts = parts.OrderBy(e => e.partId).ToArray();

        for (var i = 0; i < parts.Length; i++)
        {
            parts[i].Build(this, i);
        }
    }
}