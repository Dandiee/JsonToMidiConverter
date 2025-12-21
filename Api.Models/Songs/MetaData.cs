using Api.Generators;
using Api.Models.Serialization;

namespace Api.Models.Songs;

[AutoSerialize]
public partial class MetaData : Serializable
{
    public bool AiGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RevisionId { get; set; }
    public int SongId { get; set; }
    public string Artist { get; set; } = string.Empty;
    public int ArtistId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Author Author { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public string Restriction { get; set; } = string.Empty;
    public bool HasPlayer { get; set; }
    public bool HasTracks { get; set; }
    public bool HasChords { get; set; }
    public List<Track> Tracks { get; set; } = [];
    public int DefaultTrack { get; set; }
    public int PopularTrack { get; set; }
    public bool IsPublished { get; set; }
    public bool IsBlank { get; set; }
    public bool IsPopular { get; set; }
    public bool IsJunk { get; set; }
    public List<Video> Videos { get; set; } = [];
    public int PopularTrackGuitar { get; set; }
    public int PopularTrackBass { get; set; }
    public int PopularTrackDrum { get; set; }
    public int PrevRevisionId { get; set; }
    public List<string> Tags { get; set; } = [];
    public int Views { get; set; }
    public string Image { get; set; } = string.Empty;
    public bool Lyrics { get; set; }

    public string ModerationType { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
    public bool IsOnModeration { get; set; }
    public string CreatedVia { get; set; } = string.Empty;
    public int FavoritesCount { get; set; }
    public bool IsCollaborative { get; set; }
    public bool IsRestricted { get; set; }
    public string NextAudio { get; set; } = string.Empty;
    public string NextAudioGenerated { get; set; } = string.Empty;

    public string NextMidi { get; set; } = string.Empty;
    public string NextMidiGenerated { get; set; } = string.Empty;
    public AudioMeta? NextAudioMeta { get; set; }
    public string Audio { get; set; } = string.Empty;

    public string AudioV2 { get; set; } = string.Empty;
    public string AudioV2Generated { get; set; } = string.Empty;
    public string AudioV2Midi { get; set; } = string.Empty;
    public int AudioV2Error { get; set; }

    public string AudioV4 { get; set; } = string.Empty;
    public string AudioV4Generated { get; set; } = string.Empty;
    public string AudioV4Midi { get; set; } = string.Empty;
    public AudioMeta? AudioV4Meta { get; set; }
    public int AudioV4Error { get; set; }

    public string Error { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
}