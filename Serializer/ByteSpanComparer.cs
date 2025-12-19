namespace Serializer;

public class ByteSpanComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
{
    public static readonly ByteSpanComparer Instance = new();

    public bool Equals(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        return x.AsSpan().SequenceEqual(y);
    }

    public int GetHashCode(byte[] obj) => GetHashCode(obj.AsSpan());
    public bool Equals(ReadOnlySpan<byte> span, byte[] target) => span.SequenceEqual(target);

    public int GetHashCode(ReadOnlySpan<byte> span)
    {
        var hash = new HashCode();
        hash.AddBytes(span);
        return hash.ToHashCode();
    }

    public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();
}