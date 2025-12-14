using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter;

public record Property(PropertyInfo Prop, string Name, bool IsBool, int? MaxLength, int? BitCount, bool IsNullable);

public static class Fast
{
    private static readonly ConcurrentDictionary<Type, List<Property>> _propartyCache = new();

    public static List<Property> GetProps<T>(this T item)
        => _propartyCache.GetOrAdd(item.GetType(), type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(e => e.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .OrderBy(e => e.Name)
                .Select(prop =>
                {
                    var length = prop.GetCustomAttribute<MaxLengthAttribute>()?.Length;
                    var bitCount = length == null ? (int?)null : (int)Math.Ceiling(Math.Log2(length.Value + 1));

                    return new Property(
                        prop,
                        prop.Name,
                        prop.PropertyType == typeof(bool),
                        prop.GetCustomAttribute<MaxLengthAttribute>()?.Length,
                        bitCount,
                        Nullable.GetUnderlyingType(prop.PropertyType) != null);
                })
                .ToList());


    // Thread-safe cache to store the compiled delegates
    private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object>> _cache = new();

    /// <summary>
    /// Reads a property value from an instance using a cached compiled Expression Tree.
    /// Speed: ~10x faster than standard PropertyInfo.GetValue()
    /// </summary>
    public static object GetValue(PropertyInfo prop, object instance)
    {
        // Look up the delegate in cache, or compile it if missing
        var getter = _cache.GetOrAdd(prop, CompileGetter);
        return getter(instance);
    }

    /// <summary>
    /// Compiles a delegate (object obj) => (object)obj.Prop
    /// </summary>
    public static Func<object, object> CompileGetter(PropertyInfo prop)
    {
        // 1. Define the parameter: (object obj)
        var instanceParam = Expression.Parameter(typeof(object), "obj");

        // 2. Cast the object to its actual type: ((MyClass)obj)
        // We must cast it because 'obj' is technically an object, but the property lives on MyClass.
        var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);

        // 3. Access the property: ((MyClass)obj).MyProperty
        var propertyAccess = Expression.Property(instanceCast, prop);

        // 4. Cast the result back to object (Boxing if it's an int/bool/etc)
        // This ensures the return type is always 'object'
        var boxResult = Expression.Convert(propertyAccess, typeof(object));

        // 5. Compile to a Func<object, object>
        return Expression.Lambda<Func<object, object>>(boxResult, instanceParam).Compile();
    }
}