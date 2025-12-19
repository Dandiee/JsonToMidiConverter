using System.Text.Json.Serialization;
using Api.Models;

namespace Serializer;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNameCaseInsensitive = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
)]
[JsonSerializable(typeof(RawPart))]
[JsonSerializable(typeof(InternalDisplayText))]
[JsonSerializable(typeof(InternalTremoloBar))]
public partial class JsonContext : JsonSerializerContext{}