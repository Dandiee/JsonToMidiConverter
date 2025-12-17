using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models;

public interface ISerializable;

public class PropertyDefinition
{
    public static readonly IReadOnlyDictionary<Type, int> PackableTypes = new Dictionary<Type, int>
    {
        { typeof(bool),   1  },
        { typeof(byte),   8  },
        { typeof(sbyte),  8  },
        { typeof(short),  16 },
        { typeof(ushort), 16 },
        { typeof(int),    32 },
        { typeof(uint),   32 },
        { typeof(long),   64 },
        { typeof(ulong),  64 },
        { typeof(float),  32 },
        { typeof(double), 64 }
    };

    private static readonly ConcurrentDictionary<PropertyInfo, PropertyDefinition> DefinitionCache = new();
    private static readonly ConcurrentDictionary<Type, int> EnumSizeCache = new();

    private static readonly ConcurrentDictionary<PropertyInfo, Func<ISerializable, object?>> CompiledGetters = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Action<ISerializable, object?>> CompiledSetters = new();

    private PropertyDefinition(PropertyInfo propertyInfo)
    {
        Info = propertyInfo;
        Type = propertyInfo.PropertyType;

        EnsureType(propertyInfo.PropertyType);

        Getter = CompiledGetters.GetOrAdd(propertyInfo, CompileGetter);
        Setter = CompiledSetters.GetOrAdd(propertyInfo, CompileSetter);

        if (Type.IsEnum || PackableTypes.ContainsKey(propertyInfo.PropertyType))
        {
            IsPackable = true;
            PackSize = GetPackSizeBits(Type);
        }
    }

    private static void EnsureType(Type type)
    {
        if (Nullable.GetUnderlyingType(type) != null)
            throw new Exception("LOFASZ: Can't be Nullable<T>");

        if (type.IsInterface) throw new Exception("LOFASZ: Type must not be interface");
        if (type.IsAbstract) throw new Exception("LOFASZ: Type must not be abstract");

        if (type.IsAssignableTo(typeof(ISerializable)) || type == typeof(string))
        {
            return;
        }

        if (type.IsGenericType)
        {
            if (type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var itemType = type.GetGenericArguments()[0];
                EnsureType(itemType);
                return;
            }

            throw new Exception("LOFASZ: Only List<T> is supported as generic type");
        }

        if (type.IsEnum && Enum.GetUnderlyingType(type) != typeof(byte))
            throw new Exception($"LOFASZ: Enums must be backed by bytes");

        if (!PackableTypes.ContainsKey(type))
            throw new Exception($"LOFASZ: Not supported type!");
    }

    public static PropertyDefinition Get(PropertyInfo propertyInfo)
        => DefinitionCache.GetOrAdd(propertyInfo, new PropertyDefinition(propertyInfo));

    public PropertyInfo Info { get; }
    public Type Type { get; }

    public bool IsPackable { get; }
    public int PackSize { get; }
    public Func<ISerializable, object?> Getter { get; }
    public Action<ISerializable, object?> Setter { get; }

    private static Func<ISerializable, object?> CompileGetter(PropertyInfo prop)
    {
        var instanceParam = Expression.Parameter(typeof(ISerializable), "obj");
        var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);
        var propertyAccess = Expression.Property(instanceCast, prop);
        var boxResult = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<ISerializable, object?>>(boxResult, instanceParam).Compile();
    }

    private static Action<ISerializable, object?> CompileSetter(PropertyInfo prop)
    {
        var instanceParam = Expression.Parameter(typeof(ISerializable), "obj");
        var valueParam = Expression.Parameter(typeof(object), "value");
        var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);
        var valueCast = Expression.Convert(valueParam, prop.PropertyType);
        var assign = Expression.Assign(Expression.Property(instanceCast, prop), valueCast);
        return Expression.Lambda<Action<ISerializable, object?>>(assign, instanceParam, valueParam).Compile();
    }

    private static int GetEnumPackSize(Type type)
        => EnumSizeCache.GetOrAdd(type, t =>
        {
            var maxVal = 0;
            foreach (var val in Enum.GetValues(t))
            {
                maxVal = Math.Max(maxVal, Convert.ToByte(val));
            }

            return maxVal == 0
                ? 1
                : (int)Math.Ceiling(Math.Log2(maxVal + 1));

        });

    private static int GetPackSizeBits(Type type) => type.IsEnum ? GetEnumPackSize(type) : PackableTypes[type];

    public static int GetPackSizeBytes(Type type) => (GetPackSizeBits(type) + 7) / 8; 
}

public class ObjectDefinition
{
    private static readonly ConcurrentDictionary<Type, ObjectDefinition> Cache = new();

    public IReadOnlyList<PropertyDefinition> PackTypes { get; set; }
    public IReadOnlyList<PropertyDefinition> ComplexTypes { get; set; }
    public int TotalPackSizeBit { get; }
    public int TotalPackSizeByte { get; }

    private ObjectDefinition(Type type)
    {
        var properties = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(e => e.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .OrderBy(e => e.Name)
            .Select(PropertyDefinition.Get)
            .ToList();

        PackTypes = properties
            .Where(e => e.IsPackable)
            .ToList();

        ComplexTypes = properties
            .Where(e => !e.IsPackable)
            .ToList();

        TotalPackSizeBit = PackTypes.Sum(p => p.PackSize);
        TotalPackSizeByte = (int)Math.Ceiling(TotalPackSizeBit / 8.0);
    }



    public static ObjectDefinition Get(Type type) => Cache.GetOrAdd(type, new ObjectDefinition(type));
}

public static class DaniSerializer
{


    public static IEnumerable<byte> Serialize(ISerializable obj, Type type)
    {
        var def = ObjectDefinition.Get(type);

        foreach (var b in Pack(obj, def)) yield return b;

        foreach (var prop in def.ComplexTypes)
        {
            foreach (var b in Serialize(prop.Getter(obj), prop.Type)) yield return b;
        }
    }

    public static IEnumerable<byte> Serialize(object? value, Type type)
    {
        var isList = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        if (isList && value is IList list)
        {
            if (value == null) throw new Exception("LÜFASZ: this list cannot be null but empty!");

            foreach (var b in BitConverter.GetBytes(list.Count)) yield return b;
            var itemType = type.GetGenericArguments()[0];

            foreach (var item in list)
            {
                foreach (var b in Serialize(item, itemType)) yield return b;
            }
        }
        else if (type == typeof(string))
        {
            if (value == null) yield return 0; // null marker
            else
            {
                yield return 1; // presence marker
                var strBytes = System.Text.Encoding.UTF8.GetBytes((string)value);
                foreach (var b in BitConverter.GetBytes(strBytes.Length)) yield return b;
                foreach (var b in strBytes) yield return b;
            }
        }
        else if (type.IsAssignableTo(typeof(ISerializable)))
        {
            if (value == null) yield return 0;
            else
            {
                yield return 1; // presence marker
                foreach (var b in Serialize((ISerializable)value, type)) yield return b;
            }
        }
        else // we are inside a List<T>
        {
            var bits = ToBits(value);
            var bytes = BitConverter.GetBytes(bits);
            var bytesCount =  PropertyDefinition.GetPackSizeBytes(type);

            for (var i = 0; i < bytesCount; i++) yield return bytes[i];
        }
    }

    public static IEnumerable<byte> Pack(ISerializable obj, ObjectDefinition def)
    {
        byte currentByte = 0;
        var bitsInByte = 0;

        foreach (var prop in def.PackTypes)
        {
            var value = prop.Getter(obj)!;
            var bits = ToBits(value);

            for (var i = 0; i < prop.PackSize; i++)
            {
                var bit = (byte)((bits >> i) & 1);
                currentByte |= (byte)(bit << bitsInByte);
                bitsInByte++;

                if (bitsInByte == 8) // If buffer is full, flush it and reset
                {
                    yield return currentByte;
                    currentByte = 0;
                    bitsInByte = 0;
                }
            }
        }

        // 3. IMPORTANT: Flush any partial byte remaining after ALL properties are done
        if (bitsInByte > 0) yield return currentByte;
    }

    private static ulong ToBits(object value) => value switch
    {
        bool v => v ? 1UL : 0UL,

        byte v => v,
        sbyte v => unchecked((ulong)v),

        short v => unchecked((ulong)v),
        ushort v => v,

        int v => unchecked((ulong)v),
        uint v => v,

        long v => unchecked((ulong)v),
        ulong v => v,

        float v => (ulong)BitConverter.SingleToInt32Bits(v),
        double v => BitConverter.DoubleToUInt64Bits(v),

        _ => Convert.ToByte(value) // it cannot be anything else, but enum
    };

    static object FromBits(ulong bits, Type t)
    {
        if (t == typeof(bool)) return bits != 0;

        if (t == typeof(byte)) return (byte)bits;
        if (t == typeof(sbyte)) return unchecked((sbyte)bits);

        if (t == typeof(short)) return unchecked((short)bits);
        if (t == typeof(ushort)) return (ushort)bits;

        if (t == typeof(int)) return unchecked((int)bits);
        if (t == typeof(uint)) return (uint)bits;

        if (t == typeof(long)) return unchecked((long)bits);
        if (t == typeof(ulong)) return bits;

        if (t == typeof(float)) return BitConverter.Int32BitsToSingle((int)bits);
        if (t == typeof(double)) return BitConverter.Int64BitsToDouble((long)bits);

        if (t.IsEnum) return Enum.ToObject(t, (byte)bits);

        throw new InvalidOperationException();
    }

    private static void Unpack(IEnumerator<byte> stream, ISerializable instance, ObjectDefinition def)
    {
        var packedBuffer = ReadBytes(stream, def.TotalPackSizeByte);

        var currentBitIndex = 0;
        foreach (var prop in def.PackTypes)
        {
            ulong extractedValue = 0;

            for (var i = 0; i < prop.PackSize; i++)
            {
                // Locate the exact address
                var byteIndex = currentBitIndex / 8;
                var bitIndexInByte = currentBitIndex % 8;

                // Read the bit
                var bit = (ulong)((packedBuffer[byteIndex] >> bitIndexInByte) & 1);

                // Write the bit
                extractedValue |= (bit << i);

                currentBitIndex++;
            }

            // Ulong to target prop
            object value = FromBits(extractedValue, prop.Type);
            prop.Setter(instance, value);
        }
    }


    public static T Deserialize<T>(IEnumerator<byte> stream)
        where T : ISerializable, new()
    {
        var obj = new T();
        var type = typeof(T);

        return (T)Deserialize(type, stream, obj);
    }

    public static object Deserialize(Type type, IEnumerator<byte> stream, ISerializable obj)
    {
        var def = ObjectDefinition.Get(type);

        Unpack(stream, obj, def);

        foreach (var prop in def.ComplexTypes)
        {
            var value = ReadComplexValue(stream, prop.Type);
            prop.Setter(obj, value);
        }

        return obj;
    }

    private static object? ReadComplexValue(IEnumerator<byte> stream, Type type)
    {
        // List
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var count = BitConverter.ToInt32(ReadBytes(stream, 4), 0);
            var list = (IList)Activator.CreateInstance(type)!;
            var itemType = type.GetGenericArguments()[0];

            for (var i = 0; i < count; i++)
            {
                list.Add(ReadComplexValue(stream, itemType));
            }

            return list;
        }

        // String
        if (type == typeof(string))
        {
            var isNull = ReadByte(stream) == 0;
            if (isNull) return null;

            var length = BitConverter.ToInt32(ReadBytes(stream, 4), 0);
            return System.Text.Encoding.UTF8.GetString(ReadBytes(stream, length));
        }

        // Serializable
        if (type.IsAssignableTo(typeof(ISerializable)))
        {
            var isNull = ReadByte(stream) == 0;
            if (isNull) return null;

            return Deserialize(type, stream, (ISerializable)Activator.CreateInstance(type)!);
        }

        // We are inside a list
        var byteCount = PropertyDefinition.GetPackSizeBytes(type);
        var raw = ReadBytes(stream, byteCount);
        ulong bits = 0;
        for (int i = 0; i < byteCount; i++)
        {
            bits |= (ulong)raw[i] << (8 * i);
        }

        return FromBits(bits, type);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ReadByte(IEnumerator<byte> stream)
    {
        stream.MoveNext();
        return stream.Current;
    }

    private static byte[] ReadBytes(IEnumerator<byte> stream, int count)
    {
        var lenBytes = new byte[count];
        for (var i = 0; i < count; i++)
        {
            lenBytes[i] = ReadByte(stream);
        }

        return lenBytes;
    }

}
