using Api.Models;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Serializer;

public class ObjectDefinition
{
    private static readonly ConcurrentDictionary<Type, ObjectDefinition> Cache = new();

    public IReadOnlyList<PropertyDefinition> PackTypes { get; }
    public IReadOnlyList<PropertyDefinition> ComplexTypes { get; }
    public int TotalPackSizeBit { get; }
    public int TotalPackSizeByte { get; }

    private ObjectDefinition(Type type)
    {
        var properties = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(e => e.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .Where(e => e.PropertyType != typeof(MusicalFraction))
            .OrderBy(e => e.Name != "Notes") // make sure that the Notes property is the very first to be serialized
            .ThenBy(e => e.Name) // Deterministic order is crucial

            .Select(PropertyDefinition.Get)
            .ToList();

        PackTypes = properties.Where(e => e.IsPackable).ToList();
        ComplexTypes = properties.Where(e => !e.IsPackable).ToList();

        TotalPackSizeBit = PackTypes.Sum(p => p.Primitive.SizeInBits);
        TotalPackSizeByte = (int)Math.Ceiling(TotalPackSizeBit / 8.0);
    }

    public static ObjectDefinition Get(Type type)
    {
        if (!Cache.TryGetValue(type, out var result))
        {
            result = new ObjectDefinition(type);
            Cache.TryAdd(type, result);
        }

        return result;
    }
}