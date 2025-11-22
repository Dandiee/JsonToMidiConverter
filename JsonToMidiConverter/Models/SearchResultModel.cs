namespace JsonToMidiConverter.Models;

public class SearchResultsModel
{
    public SearchResultModel[] Records { get; set; }
}

public class SearchResultModel
{
    public int SongId { get; set; }
    public int ArtistId { get; set; }
    public string Artist { get; set; }
    public string Title { get; set; }
    public bool HasChords { get; set; }
    public bool HasPlayer { get; set; }
    public Track[] Tracks { get; set; }
    public int DefaultTrack { get; set; }
    public int PopularTrack { get; set; }
    public bool IsJunk { get; set; }
    public int PopularTrackGuitar { get; set; }
    public int PopularTrackBass { get; set; }
    public int PopularTrackDrum { get; set; }
}


