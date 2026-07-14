using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GrimorioDev.Domain.Entities;
using GrimorioDev.Infrastructure.IO;
using GrimorioDev.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Repositories;

public sealed class DataFileRepository : IDisposable
{
    private readonly string _dataFilePath;
    private readonly string _segmentsPath;
    private readonly Lz4CompressionService _compression;
    private readonly ILogger<DataFileRepository> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private FileStream? _dataStream;
    private long _currentSegmentStart;
    private int _currentSegmentIndex;
    private int _segmentSizeBytes;

    private const int EntryHeaderSize = 8;
    private const int DedupPointerSize = 16;
    private const int DedupFlag = -1;

    public DataFileRepository(
        string workspacePath,
        Lz4CompressionService compression,
        ILogger<DataFileRepository> logger,
        int segmentSizeBytes = 16 * 1024 * 1024)
    {
        _dataFilePath = Path.Combine(workspacePath, "data.lz4");
        _segmentsPath = Path.Combine(workspacePath, "segments");
        _compression = compression;
        _logger = logger;
        _segmentSizeBytes = segmentSizeBytes;

        Directory.CreateDirectory(_segmentsPath);
    }

    public long CurrentOffset => _dataStream?.Position ?? 0;
    public int CurrentSegmentIndex => _currentSegmentIndex;

    public void Open()
    {
        if (_dataStream is not null) return;

        _dataStream = new FileStream(
            _dataFilePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            bufferSize: 65536,
            useAsync: true);

        if (_dataStream.Length > 0)
        {
            _currentSegmentIndex = (int)(_dataStream.Length / _segmentSizeBytes);
            _currentSegmentStart = _currentSegmentIndex * _segmentSizeBytes;
            _dataStream.Seek(0, SeekOrigin.End);
        }
    }

    public async Task<(int SegmentIndex, long Offset)> AppendAsync(
        Guid cardId, ReadOnlyMemory<byte> payload, bool compress, CancellationToken cancellationToken = default)
    {
        byte[] compressedData;
        int decompressedSize = payload.Length;
        int compressedSize;

        if (compress && payload.Length > 8192)
        {
            var maxLen = K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(payload.Length);
            var buffer = ArrayPool<byte>.Shared.Rent(maxLen);
            try
            {
                compressedSize = _compression.Compress(payload.Span, buffer.AsSpan(0, maxLen), CompressionLevel.Fast);
                compressedData = buffer.AsSpan(0, compressedSize).ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        else
        {
            compressedData = payload.ToArray();
            compressedSize = 0;
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_dataStream is null)
                throw new InvalidOperationException("Data file not open");

            if (_currentSegmentStart + _segmentSizeBytes <= _dataStream.Position && _dataStream.Position > 0)
            {
                _currentSegmentIndex++;
                _currentSegmentStart = _dataStream.Position;
                _logger.LogDebug("New segment {Index} at offset {Offset}", _currentSegmentIndex, _currentSegmentStart);
            }

            var offset = _dataStream.Position;
            var segmentOffset = offset - _currentSegmentStart;

            var headerBytes = new byte[EntryHeaderSize];
            BitConverter.GetBytes(decompressedSize).CopyTo(headerBytes, 0);
            BitConverter.GetBytes(compressedSize).CopyTo(headerBytes, 4);
            await _dataStream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

            if (compressedSize > 0)
                await _dataStream.WriteAsync(compressedData, cancellationToken).ConfigureAwait(false);
            else
                await _dataStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);

            return (_currentSegmentIndex, segmentOffset);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<int> AppendDedupPointerAsync(byte[] contentHash, CancellationToken cancellationToken = default)
    {
        var dedupSize = -1;
        var entrySize = EntryHeaderSize + DedupPointerSize;

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_dataStream is null)
                throw new InvalidOperationException("Data file not open");

            if (_currentSegmentStart + _segmentSizeBytes <= _dataStream.Position && _dataStream.Position > 0)
            {
                _currentSegmentIndex++;
                _currentSegmentStart = _dataStream.Position;
            }

            var headerBytes = new byte[EntryHeaderSize];
            var decompressed = 0;
            BitConverter.GetBytes(decompressed).CopyTo(headerBytes, 0);
            BitConverter.GetBytes(dedupSize).CopyTo(headerBytes, 4);
            await _dataStream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
            await _dataStream.WriteAsync(contentHash.AsMemory(0, 16), cancellationToken).ConfigureAwait(false);

            return (int)(_dataStream.Position - _currentSegmentStart - entrySize);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<ReadOnlyMemory<byte>> ReadEntryAsync(
        int segmentIndex, long offsetInSegment, CancellationToken cancellationToken = default)
    {
        var segmentStart = (long)segmentIndex * _segmentSizeBytes;
        var absoluteOffset = segmentStart + offsetInSegment;

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_dataStream is null)
                throw new InvalidOperationException("Data file not open");

            var savedPosition = _dataStream.Position;
            try
            {
                _dataStream.Seek(absoluteOffset, SeekOrigin.Begin);

                var headerBytes = new byte[EntryHeaderSize];
                await _dataStream.ReadExactlyAsync(headerBytes, cancellationToken).ConfigureAwait(false);

                var decompressedSize = MemoryMarshal.Read<int>(headerBytes.AsSpan(0, 4));
                var compressedSize = MemoryMarshal.Read<int>(headerBytes.AsSpan(4, 4));

                if (compressedSize == DedupFlag)
                {
                    var hashBuffer = new byte[16];
                    await _dataStream.ReadExactlyAsync(hashBuffer, cancellationToken).ConfigureAwait(false);
                    throw new DedupReferenceException(hashBuffer);
                }

                var payloadSize = compressedSize > 0 ? compressedSize : decompressedSize;
                var payloadBuffer = new byte[payloadSize];
                await _dataStream.ReadExactlyAsync(payloadBuffer, cancellationToken).ConfigureAwait(false);

                if (compressedSize > 0)
                {
                    var decompressed = new byte[decompressedSize];
                    _compression.Decompress(payloadBuffer, decompressed);
                    return decompressed;
                }

                return payloadBuffer;
            }
            finally
            {
                _dataStream.Position = savedPosition;
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public long GetSegmentStartOffset(int segmentIndex) => (long)segmentIndex * _segmentSizeBytes;

    public void Flush()
    {
        _writeLock.Wait();
        try
        {
            _dataStream?.Flush();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _writeLock.Wait();
        try
        {
            _dataStream?.Dispose();
            _dataStream = null;
        }
        finally
        {
            _writeLock.Release();
        }
        _writeLock.Dispose();
    }
}

public sealed class DedupReferenceException : Exception
{
    public byte[] ContentHash { get; }

    public DedupReferenceException(byte[] contentHash)
        : base($"Entry is a dedup reference to blob {Convert.ToHexString(contentHash)}")
    {
        ContentHash = contentHash;
    }
}
