namespace JsonToMidiConverter.Models;

public class SearchResultsModel
{
    public SearchResultModel[] records { get; set; }
}

public class SearchResultModel
{
    public int songId { get; set; }
    public int artistId { get; set; }
    public string artist { get; set; }
    public string title { get; set; }
    public bool hasChords { get; set; }
    public bool hasPlayer { get; set; }
    public Track[] tracks { get; set; }
    public int defaultTrack { get; set; }
    public int popularTrack { get; set; }
    public bool isJunk { get; set; }
    public int popularTrackGuitar { get; set; }
    public int popularTrackBass { get; set; }
    public int popularTrackDrum { get; set; }
}


