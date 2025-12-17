using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models;

public static class IntegerTypePacker
{
    // Caches the "Schema" for each type (Property list + Bit sizes)
    private static readonly ConcurrentDictionary<Type, List<PackedProp>> SchemaCache = new();

    // -------------------------------------------------------------------------
    // 1. PUBLIC API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans the object, packs all Enums/Bools/Ints into a tight byte array.
    /// This byte array can serve as a unique "Content ID" for de-duplication.
    /// </summary>
    public static byte[] Pack(object obj)
    {
        var schema = GetSchema(obj.GetType());
        var writer = new BitWriter();

        foreach (var prop in schema)
        {
            // 1. Get Value
            var value = prop.GetValue(obj);

            // 2. Convert to UInt64 (Universal container)
            ulong intValue = 0;
            if (prop.IsEnum) intValue = Convert.ToUInt64(value);
            else if (prop.IsBool) intValue = (bool)value ? 1u : 0u;
            else intValue = Convert.ToUInt64(value);

            // 3. Write exact bits
            writer.Write(intValue, prop.BitSize);
        }

        return writer.Flush();
    }

    /// <summary>
    /// Rehydrates an object from its packed bit stream.
    /// </summary>
    public static void Unpack(object obj, byte[] data)
    {
        var schema = GetSchema(obj.GetType());
        var reader = new BitReader(data);

        foreach (var prop in schema)
        {
            // 1. Read exact bits
            ulong rawValue = reader.Read(prop.BitSize);

            // 2. Convert back to target type
            object finalValue;
            if (prop.IsEnum) finalValue = Enum.ToObject(prop.Type, rawValue);
            else if (prop.IsBool) finalValue = rawValue == 1;
            else finalValue = Convert.ChangeType(rawValue, prop.Type);

            // 3. Set Value
            prop.SetValue(obj, finalValue);
        }
    }

    // -------------------------------------------------------------------------
    // 2. SCHEMA ANALYSIS (The "Smarts")
    // -------------------------------------------------------------------------

    private record PackedProp
    {
        public string Name { get; init; }
        public Type Type { get; init; }
        public int BitSize { get; init; }
        public bool IsEnum { get; init; }
        public bool IsBool { get; init; }
        public Func<object, object> GetValue { get; init; }
        public Action<object, object> SetValue { get; init; }
    }

    private static List<PackedProp> GetSchema(Type type)
    {
        return SchemaCache.GetOrAdd(type, t =>
        {
            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null) // Skip ignored
                .Where(p => IsIntegerType(p.PropertyType)) // Only take integer types
                .OrderBy(p => p.Name) // Deterministic Order is CRITICAL for ID generation
                .ToList();

            var schema = new List<PackedProp>();

            foreach (var p in props)
            {
                var pt = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;

                int bitSize;
                bool isEnum = pt.IsEnum;
                bool isBool = pt == typeof(bool);

                if (isBool) bitSize = 1;
                else if (isEnum) bitSize = CalculateEnumBits(pt);
                else bitSize = GetPrimitiveBitSize(pt);

                schema.Add(new PackedProp
                {
                    Name = p.Name,
                    Type = pt,
                    BitSize = bitSize,
                    IsEnum = isEnum,
                    IsBool = isBool,
                    GetValue = CompileGetter(p),
                    SetValue = CompileSetter(p)
                });
            }
            return schema;
        });
    }

    private static bool IsIntegerType(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u == typeof(bool) || u.IsEnum ||
               u == typeof(byte) || u == typeof(sbyte) ||
               u == typeof(short) || u == typeof(ushort) ||
               u == typeof(int) || u == typeof(uint) ||
               u == typeof(long) || u == typeof(ulong);
    }


    private static int GetPrimitiveBitSize(Type t) => Type.GetTypeCode(t) switch
    {
        TypeCode.Byte or TypeCode.SByte => 8,
        TypeCode.Int16 or TypeCode.UInt16 => 16,
        TypeCode.Int32 or TypeCode.UInt32 => 32,
        TypeCode.Int64 or TypeCode.UInt64 => 64,
        _ => throw new InvalidOperationException($"Lofasz: Unsupported integer type: {t.FullName}")
    };

    private static int CalculateEnumBits(Type enumType)
    {
        var values = Enum.GetValues(enumType);
        if (values.Length == 0) return 0;

        // Find the absolute highest numeric value defined
        ulong maxVal = 0;
        foreach (var val in values)
        {
            ulong v = Convert.ToUInt64(val);
            if (v > maxVal) maxVal = v;
        }

        if (maxVal == 0) return 1; // At least 1 bit to store '0'

        // Log2(Max + 1) gives required bits. 
        // Example: Max=3 (11 binary) -> Log2(4) = 2 bits.
        return (int)Math.Ceiling(Math.Log2(maxVal + 1));
    }

    // -------------------------------------------------------------------------
    // 3. BIT STREAM HELPERS (Low Level)
    // -------------------------------------------------------------------------

    private class BitWriter
    {
        private readonly List<byte> _bytes = new();
        private byte _currentByte;
        private int _bitsInByte;

        public void Write(ulong value, int numBits)
        {
            for (int i = 0; i < numBits; i++)
            {
                // Take lowest bit of value
                byte bit = (byte)((value >> i) & 1);

                // Pack into current byte
                _currentByte |= (byte)(bit << _bitsInByte);
                _bitsInByte++;

                if (_bitsInByte == 8)
                {
                    _bytes.Add(_currentByte);
                    _currentByte = 0;
                    _bitsInByte = 0;
                }
            }
        }

        public byte[] Flush()
        {
            if (_bitsInByte > 0) _bytes.Add(_currentByte);
            return _bytes.ToArray();
        }
    }

    private class BitReader
    {
        private readonly byte[] _data;
        private int _byteIndex;
        private int _bitIndex;

        public BitReader(byte[] data) => _data = data;

        public ulong Read(int numBits)
        {
            ulong value = 0;
            for (int i = 0; i < numBits; i++)
            {
                if (_byteIndex >= _data.Length) return value;

                // Read bit
                ulong bit = (ulong)((_data[_byteIndex] >> _bitIndex) & 1);
                value |= (bit << i);

                _bitIndex++;
                if (_bitIndex == 8)
                {
                    _byteIndex++;
                    _bitIndex = 0;
                }
            }
            return value;
        }
    }

    // (Include your Reflection CompileGetter/Setter helpers here as before)
    // I omitted them for brevity, but you use the same ones from your snippet.
    private static Func<object, object> CompileGetter(PropertyInfo prop)
    { /* ... Your Existing Reflection Code ... */ return p => prop.GetValue(p); }
    private static Action<object, object> CompileSetter(PropertyInfo prop)
    { /* ... Your Existing Reflection Code ... */ return (o, v) => prop.SetValue(o, v); }
}