using System.Text.Json;

namespace Dani.Converter.Options;

public class DrumMapping
{
    public static readonly IReadOnlyDictionary<int, DrumMapping> Mapping;

    static DrumMapping()
    {
        var mapping = JsonSerializer.Deserialize<List<DrumMapping>>(File.ReadAllText("DrumNoteMapping.json"));
        Mapping = mapping!.ToDictionary(dm => dm.Fret);
    }

    public int Fret { get; set; }
    public int NoteNumber { get; set; }
    public string Name { get; set; }
}
