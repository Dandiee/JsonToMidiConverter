using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter;

public static class SimpleBitPacker
{
    public static byte[] Pack(object instance)
    {
        // A. Find all properties with MaxLength
        var props = instance.GetProps().Where(e => e.MaxLength.HasValue);

        // B. Calculate total bits needed
        var totalBits = props.Sum(p => p.BitCount.Value);
        var totalBytes = (totalBits + 7) / 8; // Round up to nearest byte
        var buffer = new byte[totalBytes];

        // C. Write loops
        var currentBit = 0;
        foreach (var p in props)
        {
            try
            {
                var rawValue = Fast.GetValue(p.Prop, instance)!;
                var value = Convert.ToUInt64(rawValue);

                if (value > (ulong)p.MaxLength)
                {
                    var inst = instance.GetType().Name;
                    var asd = p.Name;
                    throw new Exception($"Value {value} in {p.Prop.Name} exceeds MaxLength {p.MaxLength}");
                }

                WriteBits(buffer, currentBit, value, p.BitCount.Value);
                currentBit += p.BitCount.Value;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        return buffer;
    }

    public static void Unpack(object instance, byte[] data)
    {
        var props = instance.GetProps().Where(e  => e.MaxLength.HasValue);
        var currentBit = 0;
        foreach (var p in props)
        {
            var value = ReadBits(data, currentBit, p.BitCount.Value);
            var castValue = Convert.ChangeType(value, Enum.GetUnderlyingType(p.Prop.PropertyType) ?? p.Prop.PropertyType);

            if (p.Prop.PropertyType.IsEnum)
            {
                castValue = Enum.ToObject(p.Prop.PropertyType, value);
            }

            p.Prop.SetValue(instance, castValue);
            currentBit += p.BitCount.Value;
        }
    }

    private static void WriteBits(byte[] buffer, int bitOffset, ulong value, int bitCount)
    {
        while (bitCount > 0)
        {
            var byteIndex = bitOffset / 8;
            var bitInByte = bitOffset % 8;
            var spaceInByte = 8 - bitInByte;
            var bitsToWrite = Math.Min(bitCount, spaceInByte);

            // Create a mask for the bits we are writing now
            var chunk = (byte)(value & ((1UL << bitsToWrite) - 1));

            // Shift them to the correct position
            buffer[byteIndex] |= (byte)(chunk << bitInByte);

            // Move forward
            value >>= bitsToWrite;
            bitOffset += bitsToWrite;
            bitCount -= bitsToWrite;
        }
    }

    private static ulong ReadBits(byte[] buffer, int bitOffset, int bitCount)
    {
        ulong result = 0;
        var bitsRead = 0;

        while (bitCount > 0)
        {
            var byteIndex = bitOffset / 8;
            var bitInByte = bitOffset % 8;
            var spaceInByte = 8 - bitInByte;
            var bitsToRead = Math.Min(bitCount, spaceInByte);

            // Extract bits
            int byteVal = buffer[byteIndex];
            var chunk = (byteVal >> bitInByte) & ((1 << bitsToRead) - 1);

            // Add to result
            result |= (ulong)chunk << bitsRead;

            // Move forward
            bitsRead += bitsToRead;
            bitOffset += bitsToRead;
            bitCount -= bitsToRead;
        }
        return result;
    }
}