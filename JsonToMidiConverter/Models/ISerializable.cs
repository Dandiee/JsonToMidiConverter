using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models;

public static class DaniSerializer
{
    private record Prop(PropertyInfo Info, string Name);

    private static readonly ConcurrentDictionary<Type, List<Prop>> PropCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object>> GetterCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object>> SetterCache = new();

    private static bool GetIsDefault(object? value, Type type)
    {
        if (value is string) return true;
        if (value is IList list && list.Count == 0) return true;
        if (value is string str && string.IsNullOrEmpty(str)) return true;
        var defaultValue = type.IsValueType ? Activator.CreateInstance(type) : null;
        return Equals(defaultValue, value);
    }

    public static IEnumerable<byte> Serialize(object obj)
    {
        var props = GetProps(obj.GetType())
            .Select(prop =>
            {
                var value = GetValue(prop, obj);
                return new
                {
                    Type = prop.Info.PropertyType,
                    Value = value,
                    IsDefault = GetIsDefault(value, prop.Info.PropertyType)
                };
            }).ToList();

        var presenceByteCount = (props.Count + 7) / 8;
        var presenceBytes = new byte[presenceByteCount];
        for (var i = 0; i < props.Count; i++)
        {
            if (props[i].IsDefault)
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;

                presenceBytes[byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        foreach (var b in presenceBytes) yield return b;

        foreach (var prop in props.Where(e => !e.IsDefault))
        {
            foreach (var b in Serialize(prop.Value, prop.Type)) yield return b;
        }
    }

    private static  IEnumerable<byte> Serialize(object value, Type type)
    {
        var isList = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        if (isList && value is IList list)
        {
            foreach (var b in BitConverter.GetBytes(list.Count)) yield return b;
            var itemType = type.GetGenericArguments()[0];

            foreach (var item in list)
            {
                foreach (var b in Serialize(item, itemType)) yield return b;
            }
        }
        else if ((Nullable.GetUnderlyingType(type) ?? type).IsEnum)
        {
            yield return (byte)value;
        }
        else
        {
            foreach (var b in SerializeObject(value)) yield return b;
        }
    }

    private static IEnumerable<byte> SerializeObject(object value)
    {
        var bytes = value switch
        {
            int i => BitConverter.GetBytes(i),
            short s => BitConverter.GetBytes(s),
            ushort us => BitConverter.GetBytes(us),
            ulong ul => BitConverter.GetBytes(ul),
            long l => BitConverter.GetBytes(l),
            double d => BitConverter.GetBytes(d),
            float f => BitConverter.GetBytes(f),
            bool bo => BitConverter.GetBytes(bo),
            byte b => [b],
            sbyte sb => [(byte)sb],
            string str => SerializeString(str),
            _ => Serialize(value)
        };

        foreach (var b in bytes) yield return b;
    }

    private static IEnumerable<byte> SerializeString(string str)
    {
        var strBytes = System.Text.Encoding.UTF8.GetBytes(str);
        foreach (var b in BitConverter.GetBytes(strBytes.Length)) yield return b;
        foreach (var b in strBytes) yield return b;
    }

    public static T Deserialize<T>(byte[] data) where T : new()
        => (T)DeserializeValue(typeof(T), new Queue<byte>(data));

    private static object DeserializeValue(Type type, Queue<byte> queue)
    {
        var nonNullType = Nullable.GetUnderlyingType(type) ?? type;
        if (nonNullType.IsGenericType && nonNullType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var count = BitConverter.ToInt32(ReadBytes(queue, 4));
            var list = (IList)Activator.CreateInstance(nonNullType)!;
            var itemType = nonNullType.GetGenericArguments()[0];

            for (var i = 0; i < count; i++)
            {
                list.Add(DeserializeValue(itemType, queue));
            }
            return list;
        }

        if (nonNullType.IsEnum)
        {
            return Enum.ToObject(nonNullType, queue.Dequeue());
        }

        return Type.GetTypeCode(nonNullType) switch
        {
            TypeCode.Int32 => BitConverter.ToInt32(ReadBytes(queue, 4)),
            TypeCode.Int16 => BitConverter.ToInt16(ReadBytes(queue, 2)),
            TypeCode.UInt16 => BitConverter.ToUInt16(ReadBytes(queue, 2)),
            TypeCode.UInt64 => BitConverter.ToUInt64(ReadBytes(queue, 8)),
            TypeCode.Int64 => BitConverter.ToInt64(ReadBytes(queue, 8)),
            TypeCode.Single => BitConverter.ToSingle(ReadBytes(queue, 4)),
            TypeCode.Double => BitConverter.ToDouble(ReadBytes(queue, 8)),
            TypeCode.Boolean => BitConverter.ToBoolean(ReadBytes(queue, 1)),
            TypeCode.Byte => queue.Dequeue(),
            TypeCode.SByte => (sbyte)queue.Dequeue(),
            TypeCode.String => DeserializeString(queue),
            _ => DeserializeObject(nonNullType, queue)
        };
    }

    private static object DeserializeObject(Type type, Queue<byte> queue)
    {
        var obj = Activator.CreateInstance(type)!;
        var props = GetProps(type);

        var maskByteCount = (props.Count + 7) / 8;
        var maskBytes = ReadBytes(queue, maskByteCount);

        for (var i = 0; i < props.Count; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;

            var isDefault = (maskBytes[byteIndex] & (1 << bitIndex)) != 0;
            if (!isDefault)
            {
                SetValue(props[i], obj, DeserializeValue(props[i].Info.PropertyType, queue));
            }
        }

        return obj;
    }

    private static byte[] ReadBytes(Queue<byte> queue, int count)
    {
        var bytes = new byte[count];
        for (var i = 0; i < count; i++)
        {
            if (queue.Count == 0) throw new EndOfStreamException();

            bytes[i] = queue.Dequeue();
        }
        return bytes;
    }

    private static string DeserializeString(Queue<byte> queue)
    {
        var length = BitConverter.ToInt32(ReadBytes(queue, 4));
        var bytes = ReadBytes(queue, length);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }


    private static object GetValue(Prop prop, object instance)
        => GetterCache.GetOrAdd(prop.Info, CompileGetter)(instance);
    private static Func<object, object> CompileGetter(PropertyInfo prop)
    {
        var instanceParam = Expression.Parameter(typeof(object), "obj");
        var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);
        var propertyAccess = Expression.Property(instanceCast, prop);
        var boxResult = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<object, object>>(boxResult, instanceParam).Compile();
    }

    private static void SetValue(Prop prop, object instance, object value)
        => SetterCache.GetOrAdd(prop.Info, CompileSetter)(instance, value);
    private static Action<object, object> CompileSetter(PropertyInfo prop)
    {
        var instanceParam = Expression.Parameter(typeof(object), "obj");
        var valueParam = Expression.Parameter(typeof(object), "value");
        var instanceCast = Expression.Convert(instanceParam, prop.DeclaringType!);
        var valueCast = Expression.Convert(valueParam, prop.PropertyType);
        var assign = Expression.Assign(Expression.Property(instanceCast, prop), valueCast);
        return Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam).Compile();
    }

    private static List<Prop> GetProps(Type type)
        => PropCache.GetOrAdd(
            type,
            t => t
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(e => e.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .OrderBy(e => e.Name)
                .Select(e => new Prop(e, e.Name))
                .ToList()
        );
}
