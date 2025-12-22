using Dani.Data.Factories;
using Dani.Data.Models.Parts;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dani.Data.Serialization;

public abstract class Serializable
{
    // --- ABSTRACT METHODS ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void Read(ReadOnlySpan<byte> buffer, ref int cursor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void Write(Span<byte> buffer, ref int cursor);

    // --- PRIMITIVE WRITERS ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, byte value) => buffer[cursor++] = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, sbyte value) => buffer[cursor++] = (byte)value;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, DateTime value)
    {
        var dateBytes = (ulong)value.ToBinary();
        Write(buffer, ref cursor, dateBytes);
    }

    // --- STRING WRITER (CORRECTED) ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Write(buffer, ref cursor, (uint)0);
            return;
        }

        // INTEGRITY: Write text offset by 4 bytes to leave room for the header
        int length = System.Text.Encoding.UTF8.GetBytes(value, buffer.Slice(cursor + 4));

        Write(buffer, ref cursor, (uint)length);
        cursor += length;
    }

    // --- LIST WRITERS (INTEGRITY GUARDED) ---

    // 1. Generic Serializable Objects
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write<T>(Span<byte> buffer, ref int cursor, ICollection<T> list) where T : Serializable
    {
        // GUARD: Detect overflow. 
        // If you hit this exception, you have a list > 65535 items, which breaks your format.
        if (list.Count > ushort.MaxValue)
            throw new InvalidOperationException($"List<{typeof(T).Name}> Overflow: {list.Count} items exceeds limit of 65535.");

        Write(buffer, ref cursor, (ushort)list.Count);

        foreach (var item in list)
        {
            item.Write(buffer, ref cursor);

            // Pooling
            if (item is Note note) ThreadLocalPool<Note>.Return(note);
            else if (item is Beat beat) ThreadLocalPool<Beat>.Return(beat);
        }
    }

    // 2. Byte List (Fast Path)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, List<byte> list)
    {
        // GUARD: Essential here because of the CopyTo logic!
        if (list.Count > ushort.MaxValue)
            throw new InvalidOperationException($"List<byte> Overflow: {list.Count} items exceeds limit of 65535.");

        ushort count = (ushort)list.Count;
        Write(buffer, ref cursor, count);

        if (count > 0)
        {
            var listSpan = CollectionsMarshal.AsSpan(list);
            listSpan.CopyTo(buffer.Slice(cursor));
            cursor += count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, List<string> list)
    {
        // GUARD: Essential here because of the CopyTo logic!
        if (list.Count > ushort.MaxValue)
            throw new InvalidOperationException($"List<byte> Overflow: {list.Count} items exceeds limit of 65535.");

        ushort count = (ushort)list.Count;
        Write(buffer, ref cursor, count);

        if (count > 0)
        {
            foreach (var str in list)
            {
                Write(buffer, ref cursor, str);
            }
        }
    }

    // 3. SByte List (Standard Loop)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Write(Span<byte> buffer, ref int cursor, List<sbyte> list)
    {
        int countInt = list?.Count ?? 0;

        // GUARD
        if (countInt > ushort.MaxValue)
            throw new InvalidOperationException($"List<sbyte> Overflow: {countInt} items exceeds limit of 65535.");

        Write(buffer, ref cursor, (ushort)countInt);

        if (list != null)
        {
            foreach (var item in list)
            {
                buffer[cursor++] = (byte)item;
            }
        }
    }

    // --- READ HELPERS (UNCHANGED) ---
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
        var length = (int)ReadUInt32(buffer, ref cursor);
        if (length == 0) return string.Empty;

        string val = System.Text.Encoding.UTF8.GetString(buffer.Slice(cursor, length));
        cursor += length;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<T> ReadList<T>(ReadOnlySpan<byte> buffer, ref int cursor) where T : Serializable, new()
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<string> ReadStringList(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        var count = ReadUInt16(buffer, ref cursor);
        var list = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(ReadString(buffer, ref cursor));
        }
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected DateTime ReadDateTime(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        var dateBytes = (long)ReadUInt64(buffer, ref cursor);
        return DateTime.FromBinary(dateBytes);
    }
}