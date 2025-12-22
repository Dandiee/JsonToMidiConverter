using Dani.Data.Generators;
using Dani.Data.Serialization;

namespace Dani.Data.Models.Parts;

[AutoSerialize]
public sealed partial class NewLyric : Serializable
{
    public int Line { get; set; }
    public int Offset { get; set; }
    public string Text { get; set; } = string.Empty;
}