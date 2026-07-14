using System.Buffers;

namespace GrimorioDev.Infrastructure.IO;

public sealed class PooledBuffer : IDisposable
{
    private byte[]? _buffer;
    private int _length;
    private bool _disposed;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _length);
    public Memory<byte> WrittenMemory => _buffer.AsMemory(0, _length);
    public ArraySegment<byte> WrittenSegment => new(_buffer!, 0, _length);
    public int Length => _length;
    public byte[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(PooledBuffer));

    public static PooledBuffer Rent(int minimumLength = 4096)
    {
        return new PooledBuffer
        {
            _buffer = ArrayPool<byte>.Shared.Rent(minimumLength),
            _length = 0
        };
    }

    public void Advance(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _length += count;
    }

    public void Reset()
    {
        _length = 0;
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_buffer is null) throw new ObjectDisposedException(nameof(PooledBuffer));
        return _buffer.AsSpan(_length, _buffer.Length - _length);
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_buffer is null) throw new ObjectDisposedException(nameof(PooledBuffer));
        return _buffer.AsMemory(_length, _buffer.Length - _length);
    }

    public void Dispose()
    {
        if (!_disposed && _buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
            _disposed = true;
        }
    }
}
