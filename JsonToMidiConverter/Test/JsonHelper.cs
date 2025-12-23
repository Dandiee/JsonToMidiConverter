using System.Text;
using System.Text.Json;

namespace JsonToMidiConverter.Test;

public static class JsonHelper
{
    public static string GetString(JsonElement element, IEnumerable<string> ex)
    {
        // Use a HashSet for fast O(1) lookups
        var exclusions = new HashSet<string>(ex);

        using (var stream = new MemoryStream())
        {
            // Indented = false ensures the output is minified
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                WriteElementRecursively(element, writer, exclusions);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    private static void WriteElementRecursively(JsonElement element, Utf8JsonWriter writer, HashSet<string> exclusions)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    // This is the core logic: Only write if the name is NOT in the exclusion list
                    if (!exclusions.Contains(prop.Name))
                    {
                        writer.WritePropertyName(prop.Name);
                        // Recurse down to handle nested objects/arrays
                        WriteElementRecursively(prop.Value, writer, exclusions);
                    }
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement arrayItem in element.EnumerateArray())
                {
                    // Recurse for array items (they might contain objects with excluded props)
                    WriteElementRecursively(arrayItem, writer, exclusions);
                }
                writer.WriteEndArray();
                break;

            default:
                // For primitives (String, Number, True, False, Null), 
                // we just copy the raw value directly.
                element.WriteTo(writer);
                break;
        }
    }
}