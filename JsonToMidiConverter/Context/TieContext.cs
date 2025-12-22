namespace JsonToMidiConverter.Models.Song;

public sealed class TieContext
{
    public Nota Source { get; }
    public Nota Destination { get; }
    public IReadOnlyList<Nota> FullChain { get; }

    public TieContext(Nota destinationNote)
    {
        if (!destinationNote.Tie || destinationNote.WillBeTied) throw new Exception("no");

        var chain = new List<Nota> { destinationNote };

        while (chain[^1].Previous != null && chain[^1].Tie)
        {
            chain.Add(chain[^1].Previous!);
        }

        chain.Reverse();

        FullChain = chain;
        Source = FullChain[0];
        Destination = FullChain[^1];
    }
}