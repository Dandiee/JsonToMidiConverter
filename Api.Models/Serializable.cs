using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Api.Models;
public abstract class Serializable
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void Read(ReadOnlySpan<byte> buffer, ref int cursor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void Write(Span<byte> buffer, ref int cursor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, byte value)
    {
        // Direct assignment is fastest for single byte
        buffer[cursor] = value;
        cursor += 1; // Increment cursor, NOT value
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, ushort value)
    {
        // Slice the buffer so we write at the correct offset
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(cursor), value);
        cursor += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(cursor), value);
        cursor += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(cursor), value);
        cursor += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write<T>(Span<byte> buffer, ref int cursor, T? model)
        where T : Serializable
    {
        var isNull = model == null;
        Write(buffer, ref cursor, isNull ? (byte)0 : (byte)1);
        if (!isNull)
        {
            model!.Write(buffer, ref cursor, model);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T? Read<T>(ReadOnlySpan<byte> buffer, ref int cursor)
        where T : Serializable, new()
    {
        var isNull = ReadByte(buffer, ref cursor) == 1;
        if (isNull) return null;

        var model = new T();
        model.Read(buffer, ref cursor);
        return model;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(cursor), value);
        cursor += 8;
    }

    // --- READ HELPERS (You likely need these too!) ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected byte ReadByte(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        byte val = buffer[cursor];
        cursor += 1;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected ushort ReadUInt16(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        ushort val = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(cursor));
        cursor += 2;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected uint ReadUInt32(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        uint val = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(cursor));
        cursor += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected float ReadSingle(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        float val = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(cursor));
        cursor += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected ulong ReadUInt64(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        ulong val = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(cursor));
        cursor += 8;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, string value)
    {
        // 1. Handle empty strings efficiently
        if (string.IsNullOrEmpty(value))
        {
            buffer[cursor++] = 0; // Length = 0
            return;
        }

        // 2. Get bytes (UTF8 is standard)
        // We get the bytes directly into the span to avoid allocating an array
        int byteCount = System.Text.Encoding.UTF8.GetBytes(value, buffer.Slice(cursor + 1));

        // 3. Write Length (at cursor)
        buffer[cursor] = (byte)byteCount;

        // 4. Advance cursor (Length byte + Data bytes)
        cursor += 1 + byteCount;
    }

    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected string ReadString(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        // 1. Read Length
        byte length = buffer[cursor++];
        if (length == 0) return string.Empty;

        // 2. Decode String
        string val = System.Text.Encoding.UTF8.GetString(buffer.Slice(cursor, length));

        // 3. Advance
        cursor += length;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<T> ReadList<T>(ReadOnlySpan<byte> buffer, ref int cursor) 
        where T : Serializable, new()
    {
        var count = ReadUInt16(buffer, ref cursor);
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            var item = new T();
            item.Read(buffer, ref cursor);
            list.Add(item);
        }
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<byte> ReadList(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        var count = ReadUInt16(buffer, ref cursor);
        var list = new List<byte>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(ReadByte(buffer, ref cursor));
        }
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, ICollection<sbyte> list)
    {
        // 1. Write Count (as ushort)
        Write(buffer, ref cursor, (ushort)list.Count);

        // 2. Write Items
        foreach (var item in list)
        {
            // Cast sbyte -> byte (Bit pattern is identical)
            buffer[cursor++] = (byte)item;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<sbyte> ReadSByteList(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        var count = ReadUInt16(buffer, ref cursor);
        var list = new List<sbyte>(count);

        for (int i = 0; i < count; i++)
        {
            // Cast byte -> sbyte
            list.Add((sbyte)buffer[cursor++]);
        }
        return list;
    }

    //protected List<Serializable> Read(ReadOnlySpan<byte> buffer, ref int cursor)
    //{
    //    var count = ReadUInt16(buffer, ref cursor);
    //    var list = new List<T>
    //    foreach (var item in list)
    //    {
    //        item.Write(buffer, ref cursor);
    //    }
    //}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write<T>(Span<byte> buffer, ref int cursor, ICollection<T> list)
            where T : Serializable
    {
        Write(buffer, ref cursor, (ushort)list.Count);
        foreach (var item in list)
        {
            item.Write(buffer, ref cursor);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, ICollection<byte> list)
    {
        Write(buffer, ref cursor, list.Count);
        foreach (var item in list)
        {
            Write(buffer, ref cursor, item);
        }
    }
}
