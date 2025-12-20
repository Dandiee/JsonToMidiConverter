using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Api.Models.Mappers;

namespace Api.Models.Serialization;

public abstract class Serializable
{
    // --- ABSTRACT METHODS ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void Read(ReadOnlySpan<byte> buffer, ref int cursor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void Write(Span<byte> buffer, ref int cursor);

    // --- PRIMITIVE WRITERS ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, byte value)
    {
        buffer[cursor++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, sbyte value)
    {
        buffer[cursor++] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, ushort value)
    {
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
    protected void Write(Span<byte> buffer, ref int cursor, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(cursor), value);
        cursor += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(cursor), value);
        cursor += 4;
    }

    // --- STRING WRITER ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            buffer[cursor++] = 0; // Length = 0
            return;
        }

        // Optimization: Write directly to buffer to avoid allocation
        int byteCount = System.Text.Encoding.UTF8.GetBytes(value, buffer.Slice(cursor + 1));

        // Write Length (1 byte, assumes strings < 255 chars)
        buffer[cursor] = (byte)byteCount;

        cursor += 1 + byteCount;
    }

    // --- LIST WRITERS ---

    // 1. Generic Serializable Objects
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write<T>(Span<byte> buffer, ref int cursor, ICollection<T> list)
            where T : Serializable
    {

        if (list == null)
        {

        }

        // Write Count (ushort covers up to 65,535 items)
        Write(buffer, ref cursor, (ushort)list.Count);

        foreach (var item in list)
        {
            // Direct call: No null check, no safety byte. Generator guarantees safety.
            item.Write(buffer, ref cursor);

            // Pooling Return Logic
            if (item is Note note) ThreadLocalPool<Note>.Return(note);
            else if (item is Beat beat) ThreadLocalPool<Beat>.Return(beat);
        }
    }

    // 2. Byte List (Fast Path)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, List<byte> list)
    {

        if (list == null)
        {

        }

        ushort count = (ushort)list.Count;
        Write(buffer, ref cursor, count);

        if (count > 0)
        {
            // MEMORY COPY: Much faster than looping
            var listSpan = CollectionsMarshal.AsSpan(list);
            listSpan.CopyTo(buffer.Slice(cursor));
            cursor += count;
        }
    }

    // 3. SByte List (Standard Loop)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, List<sbyte> list)
    {
        ushort count = (ushort)(list?.Count ?? 0);
        Write(buffer, ref cursor, count);

        if (list != null)
        {
            foreach (var item in list)
            {
                buffer[cursor++] = (byte)item;
            }
        }
    }

    // --- READ HELPERS ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected byte ReadByte(ReadOnlySpan<byte> buffer, ref int cursor) => buffer[cursor++];

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
    protected ulong ReadUInt64(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        ulong val = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(cursor));
        cursor += 8;
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
    protected string ReadString(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        byte length = buffer[cursor++];
        if (length == 0) return string.Empty;

        string val = System.Text.Encoding.UTF8.GetString(buffer.Slice(cursor, length));
        cursor += length;
        return val;
    }

    // --- LIST READERS ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<T> ReadList<T>(ReadOnlySpan<byte> buffer, ref int cursor)
        where T : Serializable, new()
    {
        var count = ReadUInt16(buffer, ref cursor);
        var list = new List<T>(count); // Allocating with capacity is faster
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
            list.Add(buffer[cursor++]);
        }
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<sbyte> ReadSByteList(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        var count = ReadUInt16(buffer, ref cursor);
        var list = new List<sbyte>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add((sbyte)buffer[cursor++]);
        }
        return list;
    }
}