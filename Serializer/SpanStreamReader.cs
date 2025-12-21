namespace Serializer;

internal ref struct SpanStreamReader
{
    private readonly Stream _stream;
    private readonly byte[] _buffer;
    private int _validLength; // How many bytes in _buffer are valid data
    private int _offset;      // Current read position in _buffer

    public SpanStreamReader(Stream stream, byte[] buffer)
    {
        _stream = stream;
        _buffer = buffer;
        _offset = 0;
        _validLength = 0;

        // Initial Fill
        FillBuffer();
    }

    public bool HasData => _offset < _validLength || _stream.Position < _stream.Length;

    public ReadOnlySpan<byte> CurrentSpan => new ReadOnlySpan<byte>(_buffer, _offset, _validLength - _offset);

    public void Advance(int bytesConsumed)
    {
        _offset += bytesConsumed;

        // Safety check
        if (_offset > _validLength)
            throw new InvalidOperationException("Parser read past the end of the buffer! Buffer too small for object?");
    }

    public void EnsureBuffer()
    {
        if (_offset > _buffer.Length * 0.75)
        {
            CompactAndFill();
        }
        else if (_validLength - _offset == 0 && _stream.Position < _stream.Length)
        {
            CompactAndFill();
        }
    }

    private void FillBuffer()
    {
        // Read as much as possible into free space
        int freeSpace = _buffer.Length - _validLength;
        if (freeSpace > 0)
        {
            int read = _stream.Read(_buffer, _validLength, freeSpace);
            _validLength += read;
        }
    }

    private void CompactAndFill()
    {
        int remaining = _validLength - _offset;

        if (remaining > 0)
        {
            // Move remaining data to start of buffer
            // We use Span.CopyTo which handles overlaps correctly
            _buffer.AsSpan(_offset, remaining).CopyTo(_buffer);
        }

        _validLength = remaining;
        _offset = 0;

        FillBuffer();
    }
}