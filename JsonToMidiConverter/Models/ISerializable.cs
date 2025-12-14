using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models;
public interface ISerializable
{
    public bool GetIsDefault(object? value, Type type)
    {
        if (value is string) return true;

        if (value is IList list && list.Count == 0) return true;
        if (value is string str && string.IsNullOrEmpty(str)) return true;
        var defaultValue = type.IsValueType ? Activator.CreateInstance(type) : null;
        return Equals(defaultValue, value);
    }

    public IEnumerable<byte> Serialize()
    {
        var props = this
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(e => e.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .OrderBy(e => e.Name)
            .Select(prop =>
            {
                var value = prop.GetValue(this);

                return new
                {
                    Type = prop.PropertyType,
                    Value = value,
                    IsDefault = GetIsDefault(value, prop.PropertyType)
                };
            })
            .ToList();

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
            foreach (var b in GetBytes(prop.Value!, prop.Type)) yield return b;
        }
    }

    public IEnumerable<byte> GetBytes(object value, Type type)
    {
        var isList = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        if (isList && value is IList list)
        {
            foreach (var b in BitConverter.GetBytes(list.Count)) yield return b;
            var itemType = type.GetGenericArguments()[0];

            foreach (var item in list)
            {
                foreach (var b in GetBytes(item, itemType)) yield return b;
            }
        }
        else if ((Nullable.GetUnderlyingType(type) ?? type).IsEnum)
        {
            yield return (byte)value;
        }
        else
        {
            foreach (var b in GetBytes(value)) yield return b;
        }
    }

    public IEnumerable<byte> GetBytes(object value)
    {
        if (value is ISerializable serializable)
        {
            foreach (var b in serializable.Serialize())
            {
                yield return b;
            }

            yield break;
        }

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

            _ => throw new InvalidOperationException($"Unsupported type: {value.GetType().FullName}")
        };

        foreach (var b in bytes) yield return b;
    }

    private IEnumerable<byte> SerializeString(string str)
    {
        var strBytes = System.Text.Encoding.UTF8.GetBytes(str);
        foreach (var b in BitConverter.GetBytes(strBytes.Length)) yield return b;
        foreach (var b in strBytes) yield return b;
    }
}
public static class BinaryDeserializer
{
    public static T Deserialize<T>(byte[] data) where T : new()
    {
        var queue = new Queue<byte>(data);
        var size = ReadBytes(queue, 4);
        return (T)DeserializeValue(typeof(T), queue);
    }

    private static object DeserializeValue(Type type, Queue<byte> queue)
    {
        var underlyingType = Nullable.GetUnderlyingType(type);
        var nonNullType = underlyingType ?? type;

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

        if (typeof(ISerializable).IsAssignableFrom(nonNullType))
        {
            var obj = Activator.CreateInstance(nonNullType)!;
            var props = nonNullType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(e => e.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .OrderBy(e => e.Name)
                .ToArray();

            var maskByteCount = (props.Length + 7) / 8;
            var maskBytes = ReadBytes(queue, maskByteCount);

            for (var i = 0; i < props.Length; i++)
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;

                var isDefault = (maskBytes[byteIndex] & (1 << bitIndex)) != 0;
                if (!isDefault)
                {
                    props[i].SetValue(obj, DeserializeValue(props[i].PropertyType, queue));
                }
                else
                {
                    // Bit is 1: Value is default -> Do nothing
                    // (obj was created with 'new', so it already has defaults)
                }
            }
            return obj;
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

            _ => throw new InvalidOperationException($"Unsupported type: {nonNullType.FullName}")
        };
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
}