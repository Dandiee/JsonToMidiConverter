namespace JsonToMidiConverter.Models;


public class SongMetaDataModel
{
    public bool aiGenerated { get; set; }
    public DateTime createdAt { get; set; }
    public int revisionId { get; set; }
    public int songId { get; set; }
    public string artist { get; set; }
    public int artistId { get; set; }
    public string title { get; set; }
    public Author author { get; set; }
    public string description { get; set; }
    public string restriction { get; set; }
    public bool hasPlayer { get; set; }
    public bool hasTracks { get; set; }
    public bool hasChords { get; set; }
    public Track[] tracks { get; set; }
    public int defaultTrack { get; set; }
    public int popularTrack { get; set; }
    public bool isPublished { get; set; }
    public bool isBlank { get; set; }
    public bool isPopular { get; set; }
    public bool isJunk { get; set; }
    public Video[] videos { get; set; }
    public int popularTrackGuitar { get; set; }
    public int popularTrackBass { get; set; }
    public int popularTrackDrum { get; set; }
    public int prevRevisionId { get; set; }
    public string[] tags { get; set; }
    public int views { get; set; }
    public string image { get; set; }
    public bool lyrics { get; set; }
    public string audioV4 { get; set; }
    public string audioV4Generated { get; set; }
    public string audioV4Midi { get; set; }
    public Audiov4meta audioV4Meta { get; set; }
    public string moderationType { get; set; }
    public bool isBlocked { get; set; }
    public bool isOnModeration { get; set; }
    public string createdVia { get; set; }
    public int favoritesCount { get; set; }
    public bool isCollaborative { get; set; }
    public bool isRestricted { get; set; }
}

public class Author
{
    public int personId { get; set; }
    public string name { get; set; }
    public string profileName { get; set; }
    public bool isModerator { get; set; }
}

public class Audiov4meta
{
    public bool hasQuickMix { get; set; }
    public bool hasMixLayout { get; set; }
}

public class Video
{
    public int id { get; set; }
    public string status { get; set; }
    public string feature { get; set; }
    public string videoId { get; set; }
}


public class Track
{
    public int instrumentId { get; set; }
    public string instrument { get; set; }
    public int views { get; set; }
    public string name { get; set; }
    public int[] tuning { get; set; }
    public string hash { get; set; }
    public int difficulty { get; set; }
}
