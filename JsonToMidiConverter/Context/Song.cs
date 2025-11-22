namespace JsonToMidiConverter.Models.Song;

public sealed partial class Song
{
    public void Build()
    {
        Parts = Parts.OrderBy(e => e.PartId).ToArray();

        for (var i = 0; i < Parts.Length; i++)
        {
            Parts[i].Build(this, i);
        }
    }
}