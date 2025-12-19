using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Serializer;

public class PropertyDefinition
{
    private static readonly Dictionary<Type, Primitive> Primitives = new()
    {
        { typeof(bool),   new(1,  o => (bool)o ? 1UL : 0UL,        b => b != 0) },
        { typeof(byte),   new(8,  o => (byte)o,                    b => (byte)b) },
        { typeof(sbyte),  new(8,  o => unchecked((ulong)(sbyte)o), b => unchecked((sbyte)b)) },
        { typeof(short),  new(16, o => unchecked((ulong)(short)o), b => unchecked((short)b)) },
        { typeof(ushort), new(16, o => (ushort)o,                  b => (ushort)b) },
        { typeof(int),    new(32, o => unchecked((ulong)(int)o),   b => unchecked((int)b)) },
        { typeof(uint),   new(32, o => (uint)o,                    b => (uint)b) },
        { typeof(long),   new(64, o => unchecked((ulong)(long)o),  b => unchecked((long)b)) },
        { typeof(ulong),  new(64, o => (ulong)o,                   b => b) },


        // Float/Double use BitConverter helpers
        { typeof(float),  new(32, o => (ulong)BitConverter.SingleToInt32Bits((float)o), b => BitConverter.Int32BitsToSingle((int)b)) },
        { typeof(double), new(64, o => BitConverter.DoubleToUInt64Bits((double)o),      b => BitConverter.Int64BitsToDouble((long)b)) }
    };

    private static readonly ConcurrentDictionary<Type, Primitive> Enums = new();
    private static readonly ConcurrentDictionary<PropertyInfo, PropertyDefinition> DefinitionCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object?>> CompiledGetters = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object?>> CompiledSetters = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Func<object, ulong>> CompiledBitGetters = new();

    private PropertyDefinition(PropertyInfo propertyInfo)
    {
        Info = propertyInfo;
        Type = propertyInfo.PropertyType;

        EnsureType(propertyInfo.PropertyType);

        Getter = CompiledGetters.GetOrAdd(propertyInfo, CompileGetter);
        Setter = CompiledSetters.GetOrAdd(propertyInfo, CompileSetter);
        


        if (Type.IsEnum || Primitives.ContainsKey(propertyInfo.PropertyType))
        {
            IsPackable = true;
            Primitive = GetPrimitive(Type);
            UlongGetter = CompiledBitGetters.GetOrAdd(propertyInfo, CompileBitGetter);
        }
    }

    public static Primitive GetPrimitive(Type type)
        => type.IsEnum
            ? GetEnumPrimitive(type)
            : Primitives[type];

    private static void EnsureType(Type type)
    {
        if (Nullable.GetUnderlyingType(type) != null)
            throw new NotSupportedException($"Nullable types are not supported: {type.Name}");

        if (type.IsInterface) throw new NotSupportedException("Properties cannot be Interfaces (except generic arguments)");
        if (type.IsAbstract) throw new NotSupportedException("Properties cannot be Abstract");
        if (type == typeof(string)) return;
        if (type.IsGenericType)
        {
            if (type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var itemType = type.GetGenericArguments()[0];
                EnsureType(itemType);
                return;
            }
            throw new NotSupportedException("Only List<T> is supported as generic type");
        }

        if (type.IsEnum && Enum.GetUnderlyingType(type) != typeof(byte))
            throw new NotSupportedException("Enums must be backed by bytes");

        //if (!Primitives.ContainsKey(type) && !type.IsEnum)
        //    throw new NotSupportedException($"Not supported type: {type.Name}");
    }

    public static PropertyDefinition Get(PropertyInfo propertyInfo)
    {
        if (!DefinitionCache.TryGetValue(propertyInfo, out var definition))
        {
            definition = new PropertyDefinition(propertyInfo);
            DefinitionCache.TryAdd(propertyInfo, definition);
        }

        return definition;
    }

    public PropertyInfo Info { get; }
    public Type Type { get; }
    public bool IsPackable { get; }

    public Func<object, object?> Getter { get; }
    public Action<object, object?> Setter { get; }
    public Func<object, ulong>? UlongGetter { get; }

    public Primitive? Primitive { get; }

    private static Func<object, object?> CompileGetter(PropertyInfo prop)
    {
        var instanceParam = Expression.Parameter(typeof(object), "obj");
        var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);
        var propertyAccess = Expression.Property(instanceCast, prop);
        var boxResult = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<object, object?>>(boxResult, instanceParam).Compile();
    }

    private static Action<object, object?> CompileSetter(PropertyInfo prop)
    {
        var instanceParam = Expression.Parameter(typeof(object), "obj");
        var valueParam = Expression.Parameter(typeof(object), "value");
        var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);
        var valueCast = Expression.Convert(valueParam, prop.PropertyType);
        var assign = Expression.Assign(Expression.Property(instanceCast, prop), valueCast);
        return Expression.Lambda<Action<object, object?>>(assign, instanceParam, valueParam).Compile();
    }

    private static int GetEnumPackSize(Type type)
    {
        var maxVal = 0;
        foreach (var val in Enum.GetValues(type))
        {
            maxVal = Math.Max(maxVal, Convert.ToByte(val));
        }

        return maxVal == 0 ? 1 : (int)Math.Ceiling(Math.Log2(maxVal + 1));
    }

    private static Primitive GetEnumPrimitive(Type type)
    {
        if (!Enums.TryGetValue(type, out var primitive))
        {
            primitive = new Primitive(GetEnumPackSize(type), CreateEnumPacker(type), CreateEnumUnpacker(type));
            Enums.TryAdd(type, primitive);
        }

        return primitive;
    }

    private static Func<object, ulong> CreateEnumPacker(Type type)
    {
        var param = Expression.Parameter(typeof(object), "obj");
        var unbox = Expression.Convert(param, type);
        var toUlong = Expression.Convert(unbox, typeof(ulong));
        return Expression.Lambda<Func<object, ulong>>(toUlong, param).Compile();
    }

    private static Func<ulong, object> CreateEnumUnpacker(Type type)
    {
        var param = Expression.Parameter(typeof(ulong), "bits");
        var castUnderlying = Expression.Convert(param, Enum.GetUnderlyingType(type));
        var castEnum = Expression.Convert(castUnderlying, type);
        var box = Expression.Convert(castEnum, typeof(object));
        return Expression.Lambda<Func<ulong, object>>(box, param).Compile();
    }

    private static Func<object, ulong> CompileBitGetter(PropertyInfo prop)
    {
        var instanceParam = Expression.Parameter(typeof(object), "obj");
        var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);
        var propertyAccess = Expression.Property(instanceCast, prop);

        Expression toULong;
        var type = prop.PropertyType;

        if (type.IsEnum)
        {
            var underlying = Expression.Convert(propertyAccess, Enum.GetUnderlyingType(type));
            toULong = Expression.Convert(underlying, typeof(ulong));
        }
        else if (type == typeof(bool)) // condition ? 1UL : 0UL
        {
            toULong = Expression.Condition(
                propertyAccess,
                Expression.Constant(1UL),
                Expression.Constant(0UL)
            );
        }
        else if (type == typeof(float)) // BitConverter.SingleToInt32Bits(prop)
        {
            var method = typeof(BitConverter).GetMethod(nameof(BitConverter.SingleToInt32Bits), [typeof(float)])!;
            var asInt = Expression.Call(method, propertyAccess);
            toULong = Expression.Convert(asInt, typeof(ulong));
        }
        else if (type == typeof(double)) // BitConverter.DoubleToUInt64Bits(prop)
        {
            var method = typeof(BitConverter).GetMethod(nameof(BitConverter.DoubleToUInt64Bits), [typeof(double)])!;
            toULong = Expression.Call(method, propertyAccess);
        }
        else
        {
            // Integers (byte, int, long, etc.): Just cast directly to ulong
            // Expression.Convert is guaranteed to be unchecked. Just don't use ConvertChecked ffs.
            toULong = Expression.Convert(propertyAccess, typeof(ulong));
        }

        return Expression.Lambda<Func<object, ulong>>(toULong, instanceParam).Compile();
    }

}