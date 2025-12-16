using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song.Enums;

public enum LegatoTechnique : byte
{
    None = 0,
    HammerPull,  // Was "Hp"
    Trill        // Was "Trill"
}