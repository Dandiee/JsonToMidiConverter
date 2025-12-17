using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models;

public interface ISerializable;

public record Primitive(int SizeInBits, Func<object, ulong> Packer, Func<ulong, object> Unpacker)
{
    public readonly int SizeInBytes = (SizeInBits + 7) / 8;
}



public class PropertyDefinition
{
    // Map types to their BIT count (not bytes yet)


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

        if (Type.IsEnum || Primitives.ContainsKey(propertyInfo.PropertyType))
        {
            IsPackable = true;
            Primitive = GetPrimitive(Type);
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

        if (!Primitives.ContainsKey(type) && !type.IsEnum)
            throw new NotSupportedException($"Not supported type: {type.Name}");
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

    public Func<ISerializable, object?> Getter { get; }
    public Action<ISerializable, object?> Setter { get; }

    public Primitive? Primitive { get; }

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
            var primitive = PropertyDefinition.GetPrimitive(value!.GetType());
            var bits = primitive.Packer(value);
            var bytesCount = primitive.SizeInBytes;
            WritePrimitiveBits(stream, bits, bytesCount);
        }
    }

    private static void Pack(ISerializable obj, ObjectDefinition def, Stream stream)
    {
        if (def.PackTypes.Count == 0) return;

        var buffer = ArrayPool<byte>.Shared.Rent(def.TotalPackSizeByte);

        try
        {
            // Create a Span for safety and easy manipulation
            var span = buffer.AsSpan(0, def.TotalPackSizeByte);
            span.Clear();

            var currentBitIndex = 0;

            foreach (var prop in def.PackTypes)
            {
                var val = prop.Getter(obj)!;
                var bits = prop.Primitive.Packer(val);
                var bitCount = prop.Primitive.SizeInBits;
                while (bitCount > 0)
                {
                    var byteIndex = currentBitIndex >> 3; // divide by 8
                    var bitOffset = currentBitIndex & 7;  // modulo 8

                    span[byteIndex] |= (byte)(bits << bitOffset);

                    var bitsWritten = 8 - bitOffset;
                    if (bitsWritten > bitCount)
                        bitsWritten = bitCount;

                    bits >>= bitsWritten;
                    bitCount -= bitsWritten;
                    currentBitIndex += bitsWritten;
                }
            }

            stream.Write(buffer, 0, def.TotalPackSizeByte);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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
        for (var i = 0; i < byteCount; i++)
        {
            stream.WriteByte((byte)(bits >> (i * 8)));
        }
    }

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
            var count = ReadInt32(stream);
            var list = (IList)Activator.CreateInstance(type)!;
            var itemType = type.GetGenericArguments()[0];

            for (var i = 0; i < count; i++)
            {
                list.Add(DeserializeInternal(itemType, stream));
            }
            return list;
        }

        // String
        if (type == typeof(string))
        {
            var marker = stream.ReadByte();
            if (marker == 0) return null;

            var length = ReadInt32(stream);
            if (length == 0) return string.Empty;

            // Allocation note: for huge strings, rent a buffer
            var bytes = new byte[length];
            ReadExactly(stream, bytes, length);
            return Encoding.UTF8.GetString(bytes);
        }

        // ISerializable Object
        if (type.IsAssignableTo(typeof(ISerializable)))
        {
            var marker = stream.ReadByte();
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
        var primitive = PropertyDefinition.GetPrimitive(type);
        var byteCount = primitive.SizeInBytes;
        ulong bits = 0;
        for (var i = 0; i < byteCount; i++)
        {
            var read = stream.ReadByte();
            if (read == -1) throw new EndOfStreamException();
            bits |= (ulong)read << (i * 8);
        }
        return primitive.Unpacker(bits);
    }

    private static void Unpack(Stream stream, ISerializable instance, ObjectDefinition def)
    {
        if (def.TotalPackSizeByte == 0) return;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(def.TotalPackSizeByte);

        try
        {
            // Read exactly the number of bytes we need into the buffer
            ReadExactly(stream, buffer, def.TotalPackSizeByte);

            Span<byte> span = buffer.AsSpan(0, def.TotalPackSizeByte);

            int currentBitIndex = 0;

            foreach (var prop in def.PackTypes)
            {
                ulong extractedValue = 0;
                int bitsRemaining = prop.Primitive.SizeInBits;
                int currentShift = 0; // Tracks where to insert the next chunk into 'extractedValue'

                while (bitsRemaining > 0)
                {
                    int byteIndex = currentBitIndex >> 3; // divide by 8
                    int bitOffset = currentBitIndex & 7;  // modulo 8

                    int val = span[byteIndex];
                    val >>= bitOffset;

                    // Calculate how many bits we can grab from this byte
                    int bitsAvailable = 8 - bitOffset;
                    int bitsToTake = (bitsAvailable < bitsRemaining) ? bitsAvailable : bitsRemaining;

                    // Create a mask to isolate just the bits we want
                    int mask = (1 << bitsToTake) - 1;

                    // Extract the chunk and cast to ulong
                    var chunk = (ulong)(val & mask);
                    extractedValue |= chunk << currentShift;

                    currentShift += bitsToTake;
                    bitsRemaining -= bitsToTake;
                    currentBitIndex += bitsToTake;
                }

                prop.Setter(instance, prop.Primitive.Unpacker(extractedValue));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadInt32(Stream stream)
    {
        var b1 = stream.ReadByte();
        var b2 = stream.ReadByte();
        var b3 = stream.ReadByte();
        var b4 = stream.ReadByte();

        if ((b1 | b2 | b3 | b4) < 0) throw new EndOfStreamException();

        return b1 | (b2 << 8) | (b3 << 16) | (b4 << 24);
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int count)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = stream.Read(buffer, offset, count - offset);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}