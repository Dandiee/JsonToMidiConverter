using System.Text.Json.Serialization;
using Api.Models.Parts;
using Api.Models.Songs;

namespace Api.Models;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNameCaseInsensitive = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
)]
[JsonSerializable(typeof(Part))]
[JsonSerializable(typeof(InternalDisplayText))]
[JsonSerializable(typeof(InternalTremoloBar))]
[JsonSerializable(typeof(RawBeat))]
[JsonSerializable(typeof(RawNote))]
[JsonSerializable(typeof(MetaData))]
public partial class JsonContext : JsonSerializerContext{}