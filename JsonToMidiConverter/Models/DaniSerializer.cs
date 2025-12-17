using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models;

public interface ISerializable;

public static class DaniSerializer
{
    private record Prop(
        PropertyInfo Info,
        bool IsBoolean,
        int? BitSize);

    private static readonly ConcurrentDictionary<Type, List<Prop>> PropCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Func<ISerializable, object>> GetterCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Action<ISerializable, object>> SetterCache = new();

    public static IEnumerable<byte> Serialize(ISerializable obj, Type type)
    {
        foreach (var b in Pack(obj)) yield return b;

        foreach (var prop in GetProps(type).Where(e => !e.BitSize.HasValue))
        {
            foreach (var b in Serialize(GetValue(prop, obj), prop.Info.PropertyType)) yield return b;
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
                foreach (var b in Serialize((ISerializable)value!, type)) yield return b;
            }
        }

        else throw new Exception("LOFASZ: ez a type nemjü");
    }

    public static IEnumerable<byte> Pack(ISerializable obj)
    {
        byte currentByte = 0;
        var bitsInByte = 0;

        // 1. Loop through all Packable properties
        foreach (var prop in GetProps(obj.GetType()).Where(e => e.BitSize.HasValue))
        {
            var value = GetValue(prop, obj);

            ulong ulongValue;

            // 1. Handle types explicitly to preserve bit patterns
            if (prop.IsBoolean) ulongValue = (bool)value ? 1u : 0u;
            else if (value is float f) ulongValue = (ulong)BitConverter.SingleToInt32Bits(f);
            else if (value is double d) ulongValue = (ulong)BitConverter.DoubleToInt64Bits(d);
            else ulongValue = Convert.ToUInt64(value);

            for (var i = 0; i < prop.BitSize!.Value; i++)
            {
                var bit = (byte)((ulongValue >> i) & 1);
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






    private static void Unpack(IEnumerator<byte> stream, Type type, ISerializable instance)
    {
        var packableProps = GetProps(type).Where(e => e.BitSize.HasValue).ToList();

        var totalBits = packableProps.Sum(p => p.BitSize!.Value);
        var totalBytes = (int)Math.Ceiling(totalBits / 8.0);
        var packedBuffer = ReadBytes(stream, totalBytes);

        var currentBitIndex = 0;
        foreach (var prop in packableProps)
        {
            ulong extractedValue = 0;
            var bitsToRead = prop.BitSize!.Value;

            for (var i = 0; i < bitsToRead; i++)
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
            object finalValue;
            var t = prop.Info.PropertyType;

            if (prop.IsBoolean) finalValue = extractedValue == 1;
            else if (t == typeof(float)) finalValue = BitConverter.Int32BitsToSingle((int)extractedValue);
            else if (t == typeof(double)) finalValue = BitConverter.Int64BitsToDouble((long)extractedValue);
            else if (t.IsEnum) finalValue = Enum.ToObject(t, extractedValue);
            else finalValue = Convert.ChangeType(extractedValue, t);

            SetValue(prop, instance, finalValue);
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
        Unpack(stream, type, obj);

        var complexProps = GetProps(type).Where(e => !e.BitSize.HasValue).ToList();

        foreach (var prop in complexProps)
        {
            var value = ReadComplexValue(stream, prop.Info.PropertyType);
            SetValue(prop, obj, value!);
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

        throw new Exception($"LOFASZ: Unknown type {type.Name}");
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












    private static object GetValue(Prop prop, ISerializable instance) => GetterCache.GetOrAdd(prop.Info, CompileGetter)(instance);

    private static Func<ISerializable, object> CompileGetter(PropertyInfo prop)
    {
        var instanceParam = Expression.Parameter(typeof(object), "obj");
        var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);
        var propertyAccess = Expression.Property(instanceCast, prop);
        var boxResult = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<object, object>>(boxResult, instanceParam).Compile();
    }

    private static void SetValue(Prop prop, ISerializable instance, object value) => SetterCache.GetOrAdd(prop.Info, CompileSetter)(instance, value);
    private static Action<ISerializable, object> CompileSetter(PropertyInfo prop)
    {
        var instanceParam = Expression.Parameter(typeof(object), "obj");
        var valueParam = Expression.Parameter(typeof(object), "value");
        var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);
        var valueCast = Expression.Convert(valueParam, prop.PropertyType);
        var assign = Expression.Assign(Expression.Property(instanceCast, prop), valueCast);
        return Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam).Compile();
    }

    private static int? GetBitSize(Type t)
    {
        if (t == typeof(bool)) return 1;
        if (t == typeof(byte) || t == typeof(sbyte)) return 8;
        if (t == typeof(short) || t == typeof(ushort)) return 16;
        if (t == typeof(int) || t == typeof(uint) || t == typeof(float)) return 32;
        if (t == typeof(long) || t == typeof(ulong) || t == typeof(double)) return 64;
        if (t == typeof(string) || t.IsAssignableTo(typeof(IList)) ||
            t.IsAssignableTo(typeof(ISerializable))) return null;
        if (t.IsEnum)
        {
            var maxVal = 0;
            foreach (var val in Enum.GetValues(t))
            {
                maxVal = Math.Max(maxVal, Convert.ToByte(val));
            }

            return maxVal == 0
                ? 1
                : (int)Math.Ceiling(Math.Log2(maxVal + 1));
        }

        throw new Exception("LOFASZ: mi ez a type");
    }

    private static List<Prop> GetProps(Type type)
        => PropCache.GetOrAdd(
            type,
            t => t
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(e => e.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .OrderBy(e => e.Name)
                .Select(e => new Prop(e,
                    IsBoolean: e.PropertyType == typeof(bool),
                    BitSize: GetBitSize(e.PropertyType)))
                .ToList()
        );
}
