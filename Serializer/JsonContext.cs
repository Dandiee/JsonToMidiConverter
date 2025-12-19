using System.Text.Json.Serialization;
using Api.Models;

namespace Serializer;

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
public partial class JsonContext : JsonSerializerContext{}