namespace JsonToMidiConverter.Models;


public class SongMetaDataModel
{
    public bool AiGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RevisionId { get; set; }
    public int SongId { get; set; }
    public string Artist { get; set; }
    public int ArtistId { get; set; }
    public string Title { get; set; }
    public Author Author { get; set; }
    public string Description { get; set; }
    public string Restriction { get; set; }
    public bool HasPlayer { get; set; }
    public bool HasTracks { get; set; }
    public bool HasChords { get; set; }
    public Track[] Tracks { get; set; }
    public int DefaultTrack { get; set; }
    public int PopularTrack { get; set; }
    public bool IsPublished { get; set; }
    public bool IsBlank { get; set; }
    public bool IsPopular { get; set; }
    public bool IsJunk { get; set; }
    public Video[] Videos { get; set; }
    public int PopularTrackGuitar { get; set; }
    public int PopularTrackBass { get; set; }
    public int PopularTrackDrum { get; set; }
    public int PrevRevisionId { get; set; }
    public string[] Tags { get; set; }
    public int Views { get; set; }
    public string Image { get; set; }
    public bool Lyrics { get; set; }
    public string AudioV4 { get; set; }
    public string AudioV4Generated { get; set; }
    public string AudioV4Midi { get; set; }
    public Audiov4Meta AudioV4Meta { get; set; }
    public string ModerationType { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsOnModeration { get; set; }
    public string CreatedVia { get; set; }
    public int FavoritesCount { get; set; }
    public bool IsCollaborative { get; set; }
    public bool IsRestricted { get; set; }
    public string? NextAudio { get; set; }
    public string? NextAudioGenerated { get; set; }

    public string? NextMidi { get; set; }
    public string? NextMidiGenerated { get; set; }
    public AudioMeta NextAudioMeta { get; set; }

}


public  class AudioMeta
{
    public bool HasQuickMix { get; set; }
    public bool HasMixLayout { get; set; }
}

public class Author
{
    public int PersonId { get; set; }
    public string Name { get; set; }
    public string ProfileName { get; set; }
    public bool IsModerator { get; set; }
}

public class Audiov4Meta
{
    public bool HasQuickMix { get; set; }
    public bool HasMixLayout { get; set; }
}

public class Video
{
    public int Id { get; set; }
    public string Status { get; set; }
    public string Feature { get; set; }
    public string VideoId { get; set; }
}


public class Track
{
    public int InstrumentId { get; set; }
    public string Instrument { get; set; }
    public int Views { get; set; }
    public string Name { get; set; }
    public int[] Tuning { get; set; }
    public string Hash { get; set; }
    public int Difficulty { get; set; }
    public bool IsVocalTrack { get; set; }
}
