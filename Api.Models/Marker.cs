using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Models.Converters;

namespace Api.Models;

[JsonConverter(typeof(MarkerConverter))]
public sealed class Marker
{
    public string Text { get; set; } = string.Empty;
    public short Width { get; set; }
}
