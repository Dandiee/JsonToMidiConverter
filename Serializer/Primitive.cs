namespace Serializer;

public record Primitive(int SizeInBits, Func<object, ulong> Packer, Func<ulong, object> Unpacker)
{
    public readonly int SizeInBytes = (SizeInBits + 7) / 8;
}