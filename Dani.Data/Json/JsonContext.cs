using System.Text.Json.Serialization;
using Dani.Data.Models.Parts;
using Dani.Data.Models.Songs;

namespace Dani.Data.Json;

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