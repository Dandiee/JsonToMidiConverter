using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models;

public interface ISerializable;

public class PropertyDefinition
{
    // Map types to their BIT count (not bytes yet)
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

    // Cache the delegates
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
            throw new NotSupportedException($"Nullable types are not supported: {type.Name}");

        if (type.IsInterface) throw new NotSupportedException("Properties cannot be Interfaces (except generic arguments)");
        if (type.IsAbstract) throw new NotSupportedException("Properties cannot be Abstract");

        if (type.IsAssignableTo(typeof(ISerializable)) || type == typeof(string)) return;

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

        if (!PackableTypes.ContainsKey(type) && !type.IsEnum)
            throw new NotSupportedException($"Not supported type: {type.Name}");
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
                maxVal = Math.Max(maxVal, Convert.ToByte(val));

            return maxVal == 0 ? 1 : (int)Math.Ceiling(Math.Log2(maxVal + 1));
        });

    public static int GetPackSizeBits(Type type) => type.IsEnum ? GetEnumPackSize(type) : PackableTypes[type];
    public static int GetPackSizeBytes(Type type) => (GetPackSizeBits(type) + 7) / 8;
}

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
            .OrderBy(e => e.Name) // Deterministic order is crucial
            .Select(PropertyDefinition.Get)
            .ToList();

        PackTypes = properties.Where(e => e.IsPackable).ToList();
        ComplexTypes = properties.Where(e => !e.IsPackable).ToList();

        TotalPackSizeBit = PackTypes.Sum(p => p.PackSize);
        TotalPackSizeByte = (int)Math.Ceiling(TotalPackSizeBit / 8.0);
    }

    public static ObjectDefinition Get(Type type) => Cache.GetOrAdd(type, new ObjectDefinition(type));
}

public static class DaniSerializer
{
    // Entry point
    public static void Serialize(ISerializable obj, Stream stream)
    {
        SerializeInternal(obj, obj.GetType(), stream);
    }

    private static void SerializeInternal(object? value, Type type, Stream stream)
    {
        var isList = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        if (isList && value is IList list)
        {
            if (value == null) throw new InvalidOperationException("Lists cannot be null");

            // Write List Count
            WriteInt32(stream, list.Count);

            var itemType = type.GetGenericArguments()[0];
            foreach (var item in list)
            {
                SerializeInternal(item, itemType, stream);
            }
        }
        else if (type == typeof(string))
        {
            if (value == null)
            {
                stream.WriteByte(0); // Null marker
            }
            else
            {
                stream.WriteByte(1); // Presence marker
                var str = (string)value;
                var strBytes = Encoding.UTF8.GetBytes(str);
                WriteInt32(stream, strBytes.Length);
                stream.Write(strBytes, 0, strBytes.Length);
            }
        }
        else if (type.IsAssignableTo(typeof(ISerializable)))
        {
            if (value == null)
            {
                stream.WriteByte(0);
            }
            else
            {
                stream.WriteByte(1);
                var def = ObjectDefinition.Get(type);

                // Pack primitives
                Pack((ISerializable)value, def, stream);

                // Recurse complex
                foreach (var prop in def.ComplexTypes)
                {
                    SerializeInternal(prop.Getter((ISerializable)value), prop.Type, stream);
                }
            }
        }
        else
        {
            // Inside a List<Primitive> or List<Enum>
            // Note: We do NOT bit-pack items in a list against each other, 
            // we treat them as individual byte-aligned entities.
            var bits = ToBits(value!);
            var bytesCount = PropertyDefinition.GetPackSizeBytes(type);
            WritePrimitiveBits(stream, bits, bytesCount);
        }
    }

    private static void Pack(ISerializable obj, ObjectDefinition def, Stream stream)
    {
        if (def.PackTypes.Count == 0) return;

        // Allocation optimization: Use a buffer from the pool or stack
        // If the pack size is small (e.g. < 256 bytes), simple array is fine. 
        // For strict 0-alloc, use Span<byte> with stackalloc if unsafe is allowed, 
        // or just a recycled buffer. keeping it simple here:
        byte[] buffer = new byte[def.TotalPackSizeByte];

        int currentBitIndex = 0;

        foreach (var prop in def.PackTypes)
        {
            var val = prop.Getter(obj)!;
            ulong bits = ToBits(val);

            // We write bits into the byte array manually
            for (int i = 0; i < prop.PackSize; i++)
            {
                if (((bits >> i) & 1) == 1)
                {
                    int bytePos = currentBitIndex / 8;
                    int bitPos = currentBitIndex % 8;
                    buffer[bytePos] |= (byte)(1 << bitPos);
                }
                currentBitIndex++;
            }
        }

        stream.Write(buffer, 0, buffer.Length);
    }

    // --- Serialization Helpers (Little Endian) ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt32(Stream stream, int value)
    {
        // Little Endian
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 24));
    }

    private static void WritePrimitiveBits(Stream stream, ulong bits, int byteCount)
    {
        for (int i = 0; i < byteCount; i++)
        {
            stream.WriteByte((byte)(bits >> (i * 8)));
        }
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
        _ => Convert.ToByte(value) // Enums
    };

    // --- Deserialization ---

    public static T Deserialize<T>(Stream stream) where T : ISerializable, new()
    {
        return (T)DeserializeInternal(typeof(T), stream);
    }

    private static object? DeserializeInternal(Type type, Stream stream)
    {
        // List
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            int count = ReadInt32(stream);
            var list = (IList)Activator.CreateInstance(type)!;
            var itemType = type.GetGenericArguments()[0];

            for (int i = 0; i < count; i++)
            {
                list.Add(DeserializeInternal(itemType, stream));
            }
            return list;
        }

        // String
        if (type == typeof(string))
        {
            int marker = stream.ReadByte();
            if (marker == 0) return null;

            int length = ReadInt32(stream);
            if (length == 0) return string.Empty;

            // Allocation note: for huge strings, rent a buffer
            byte[] bytes = new byte[length];
            ReadExactly(stream, bytes, length);
            return Encoding.UTF8.GetString(bytes);
        }

        // ISerializable Object
        if (type.IsAssignableTo(typeof(ISerializable)))
        {
            int marker = stream.ReadByte();
            if (marker == 0) return null;

            var obj = (ISerializable)Activator.CreateInstance(type)!;
            var def = ObjectDefinition.Get(type);

            Unpack(stream, obj, def);

            foreach (var prop in def.ComplexTypes)
            {
                var val = DeserializeInternal(prop.Type, stream);
                prop.Setter(obj, val);
            }
            return obj;
        }

        // Primitive in a list
        int byteCount = PropertyDefinition.GetPackSizeBytes(type);
        ulong bits = 0;
        for (int i = 0; i < byteCount; i++)
        {
            int read = stream.ReadByte();
            if (read == -1) throw new EndOfStreamException();
            bits |= (ulong)read << (i * 8);
        }
        return FromBits(bits, type);
    }

    private static void Unpack(Stream stream, ISerializable instance, ObjectDefinition def)
    {
        if (def.TotalPackSizeByte == 0) return;

        byte[] packedBuffer = new byte[def.TotalPackSizeByte];
        ReadExactly(stream, packedBuffer, def.TotalPackSizeByte);

        int currentBitIndex = 0;
        foreach (var prop in def.PackTypes)
        {
            ulong extractedValue = 0;
            for (int i = 0; i < prop.PackSize; i++)
            {
                int byteIndex = currentBitIndex / 8;
                int bitIndexInByte = currentBitIndex % 8;

                ulong bit = (ulong)((packedBuffer[byteIndex] >> bitIndexInByte) & 1);
                extractedValue |= (bit << i);
                currentBitIndex++;
            }

            prop.Setter(instance, FromBits(extractedValue, prop.Type));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadInt32(Stream stream)
    {
        int b1 = stream.ReadByte();
        int b2 = stream.ReadByte();
        int b3 = stream.ReadByte();
        int b4 = stream.ReadByte();

        if ((b1 | b2 | b3 | b4) < 0) throw new EndOfStreamException();

        return b1 | (b2 << 8) | (b3 << 16) | (b4 << 24);
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    private static object FromBits(ulong bits, Type t)
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

        throw new InvalidOperationException($"Unknown type {t.Name}");
    }
}