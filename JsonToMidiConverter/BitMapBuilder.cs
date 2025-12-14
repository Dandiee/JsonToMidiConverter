using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

// For [JsonIgnore]

namespace JsonToMidiConverter;

public static class SimpleBooleanPacker
{
    // We cache the list of properties per type so we don't scan reflection every time.
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _cache = new();

    /// <summary>
    /// Packs all boolean properties into a tight byte array.
    /// </summary>
    public static byte[] Pack(object instance)
    {
        var props = instance.GetProps().Where(e => e.IsBool).ToList();

        // Calculate size: 18 bools -> 3 bytes
        int byteCount = (props.Count + 7) / 8;
        byte[] buffer = new byte[byteCount];

        for (int i = 0; i < props.Count; i++)
        {
            // Get value (The "Slow" Reflection part)
            bool isTrue = (bool)Fast.GetValue(props[i].Prop, instance)!;

            if (isTrue)
            {
                int byteIndex = i / 8;
                int bitIndex = i % 8;

                // Set the bit
                buffer[byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        return buffer;
    }

    /// <summary>
    /// Reads bytes and sets the boolean properties on the instance.
    /// </summary>
    public static void Unpack(object instance, byte[] data)
    {
        var props = instance.GetProps().Where(e => e.IsBool).ToList();

        for (int i = 0; i < props.Count; i++)
        {
            int byteIndex = i / 8;
            int bitIndex = i % 8;

            // Check if bit is set
            bool isSet = (data[byteIndex] & (1 << bitIndex)) != 0;

            // Set value
            props[i].Prop.SetValue(instance, isSet);
        }
    }
}