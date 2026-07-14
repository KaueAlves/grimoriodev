using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
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
    private readonly object _writeLock = new();
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
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        if (_dataStream.Length > 0)
        {
            _currentSegmentIndex = (int)(_dataStream.Length / _segmentSizeBytes);
            _currentSegmentStart = _currentSegmentIndex * _segmentSizeBytes;
            _dataStream.Seek(0, SeekOrigin.End);
        }
    }

    public Task<(int SegmentIndex, long Offset)> AppendAsync(
        Guid cardId, ReadOnlyMemory<byte> payload, bool compress)
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

        var entrySize = EntryHeaderSize + (compressedSize > 0 ? compressedSize : decompressedSize);

        lock (_writeLock)
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

            Span<byte> header = stackalloc byte[EntryHeaderSize];
            BitConverter.GetBytes(decompressedSize).CopyTo(header);
            BitConverter.GetBytes(compressedSize).CopyTo(header[4..]);
            _dataStream.Write(header);

            if (compressedSize > 0)
                _dataStream.Write(compressedData);
            else
                _dataStream.Write(payload.Span);

            return Task.FromResult((_currentSegmentIndex, segmentOffset));
        }
    }

    public Task<int> AppendDedupPointerAsync(byte[] contentHash)
    {
        var dedupSize = -1;
        var entrySize = EntryHeaderSize + DedupPointerSize;

        lock (_writeLock)
        {
            if (_dataStream is null)
                throw new InvalidOperationException("Data file not open");

            if (_currentSegmentStart + _segmentSizeBytes <= _dataStream.Position && _dataStream.Position > 0)
            {
                _currentSegmentIndex++;
                _currentSegmentStart = _dataStream.Position;
            }

            Span<byte> header = stackalloc byte[EntryHeaderSize];
            var decompressed = 0;
            BitConverter.GetBytes(decompressed).CopyTo(header);
            BitConverter.GetBytes(dedupSize).CopyTo(header[4..]);
            _dataStream.Write(header);
            _dataStream.Write(contentHash.AsSpan(0, 16));

            return Task.FromResult((int)(_dataStream.Position - _currentSegmentStart - entrySize));
        }
    }

    public Task<ReadOnlyMemory<byte>> ReadEntryAsync(
        int segmentIndex, long offsetInSegment)
    {
        var segmentStart = (long)segmentIndex * _segmentSizeBytes;
        var absoluteOffset = segmentStart + offsetInSegment;

        using var mmf = MemoryMappedFile.CreateFromFile(_dataFilePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        using var accessor = mmf.CreateViewAccessor(absoluteOffset, _segmentSizeBytes, MemoryMappedFileAccess.Read);

        var headerBuffer = new byte[EntryHeaderSize];
        accessor.ReadArray(0, headerBuffer, 0, EntryHeaderSize);

        var decompressedSize = MemoryMarshal.Read<int>(headerBuffer.AsSpan(0, 4));
        var compressedSize = MemoryMarshal.Read<int>(headerBuffer.AsSpan(4, 4));

        if (compressedSize == DedupFlag)
        {
            var hashBuffer = new byte[16];
            accessor.ReadArray(EntryHeaderSize, hashBuffer, 0, 16);
            throw new DedupReferenceException(hashBuffer);
        }

        var payloadSize = compressedSize > 0 ? compressedSize : decompressedSize;
        var payloadBuffer = new byte[payloadSize];
        accessor.ReadArray(EntryHeaderSize, payloadBuffer, 0, payloadSize);

        if (compressedSize > 0)
        {
            var decompressed = new byte[decompressedSize];
            _compression.Decompress(payloadBuffer, decompressed);
            return Task.FromResult<ReadOnlyMemory<byte>>(decompressed);
        }

        return Task.FromResult<ReadOnlyMemory<byte>>(payloadBuffer);
    }

    public long GetSegmentStartOffset(int segmentIndex) => (long)segmentIndex * _segmentSizeBytes;

    public void Flush()
    {
        lock (_writeLock)
        {
            _dataStream?.Flush();
        }
    }

    public void Dispose()
    {
        lock (_writeLock)
        {
            _dataStream?.Dispose();
            _dataStream = null;
        }
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
