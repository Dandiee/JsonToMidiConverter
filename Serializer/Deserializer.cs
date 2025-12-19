using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;

namespace Serializer;

public class Deserializer
{
    public T Deserialize<T>(Stream stream) where T : new()
    {
        return (T)DeserializeInternal(typeof(T), stream);
    }

    private object? DeserializeInternal(Type type, Stream stream)
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
        if (!type.IsValueType)
        {
            var marker = stream.ReadByte();
            if (marker == 0) return null;

            var obj = Activator.CreateInstance(type)!;
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

    public void Unpack(Stream stream, object instance, ObjectDefinition def)
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

                var vaaaaaaaalue = prop.Primitive.Unpacker(extractedValue);

                // if (prop.Type == typeof(double))
                // {
                //     var key = $"{prop.Info.DeclaringType.Name} - {prop.Info.Name}";
                //     if (!Ranges.TryGetValue(key, out var range))
                //     {
                //         range = new Range(key);
                //         Ranges.TryAdd(key, range);
                //     }
                // 
                //     range.Update((double)vaaaaaaaalue);
                // }

                prop.Setter(instance, vaaaaaaaalue);
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