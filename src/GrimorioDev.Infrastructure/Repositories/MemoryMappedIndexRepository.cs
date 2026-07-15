using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using GrimorioDev.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Repositories;

public sealed class MemoryMappedIndexRepository : IDisposable
{
    private readonly string _indexPath;
    private readonly ILogger<MemoryMappedIndexRepository> _logger;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private readonly object _lock = new();
    private int _entryCount;
    private int _segmentCount;
    private long _totalDataSize;
    private bool _dirty;

    public const int EntrySize = 28;
    public const int HeaderSize = 40;
    private const uint Magic = 0x4752494D;
    private const int CurrentVersion = 1;

    public int EntryCount => _entryCount;
    public int SegmentCount => _segmentCount;
    public bool IsOpen => _accessor is not null;

    public MemoryMappedIndexRepository(string workspacePath, ILogger<MemoryMappedIndexRepository> logger)
    {
        _indexPath = Path.Combine(workspacePath, "idx.bin");
        _logger = logger;
    }

    public void Open()
    {
        if (_accessor is not null) return;

        var exists = File.Exists(_indexPath);
        if (!exists)
        {
            CreateNewIndex();
        }

        _mmf = MemoryMappedFile.CreateFromFile(_indexPath, FileMode.Open, null, 0, MemoryMappedFileAccess.ReadWrite);
        _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

        if (exists)
        {
            ReadHeader();
            _logger.LogDebug("Opened index: {Entries} entries, {Segments} segments", _entryCount, _segmentCount);
        }
        else
        {
            WriteHeader();
        }

        PrefaultPages();
    }

    private void CreateNewIndex()
    {
        using var fs = new FileStream(_indexPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096);
        var header = new byte[HeaderSize];
        BitConverter.GetBytes(Magic).CopyTo(header, 0);
        BitConverter.GetBytes(CurrentVersion).CopyTo(header, 4);
        fs.Write(header);
    }

    private void ReadHeader()
    {
        if (_accessor is null) return;

        var magic = _accessor.ReadUInt32(0);
        if (magic != Magic)
            throw new InvalidDataException($"Invalid index file magic: 0x{magic:X8}");

        var version = _accessor.ReadInt32(4);
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported index version: {version}");

        _entryCount = _accessor.ReadInt32(8);
        _segmentCount = _accessor.ReadInt32(12);
        _totalDataSize = _accessor.ReadInt64(16);
    }

    private void WriteHeader()
    {
        if (_accessor is null) return;

        var headerBytes = new byte[HeaderSize];
        BitConverter.GetBytes(Magic).CopyTo(headerBytes, 0);
        BitConverter.GetBytes(CurrentVersion).CopyTo(headerBytes, 4);
        BitConverter.GetBytes(_entryCount).CopyTo(headerBytes, 8);
        BitConverter.GetBytes(_segmentCount).CopyTo(headerBytes, 12);
        BitConverter.GetBytes(_totalDataSize).CopyTo(headerBytes, 16);
        _accessor.WriteArray(0, headerBytes, 0, HeaderSize);
        _dirty = true;
    }

    private static Guid ReadGuid(MemoryMappedViewAccessor accessor, long offset)
    {
        var bytes = new byte[16];
        accessor.ReadArray(offset, bytes, 0, 16);
        return new Guid(bytes);
    }

    private static void WriteGuid(MemoryMappedViewAccessor accessor, long offset, Guid value)
    {
        var bytes = value.ToByteArray();
        accessor.WriteArray(offset, bytes, 0, 16);
    }

    public bool TryFind(Guid cardId, out int segmentIndex, out long offsetInSegment)
    {
        segmentIndex = 0;
        offsetInSegment = 0;

        if (_accessor is null || _entryCount == 0)
            return false;

        var lo = 0;
        var hi = _entryCount - 1;
        var entriesStart = HeaderSize;

        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            var entryOffset = entriesStart + (long)mid * EntrySize;
            var midId = ReadGuid(_accessor, entryOffset);
            var cmp = cardId.CompareTo(midId);

            if (cmp == 0)
            {
                segmentIndex = _accessor.ReadInt32(entryOffset + 16);
                offsetInSegment = _accessor.ReadInt64(entryOffset + 20);
                return true;
            }

            if (cmp < 0)
                hi = mid - 1;
            else
                lo = mid + 1;
        }

        return false;
    }

    public bool TryFindSimd(Guid cardId, out int segmentIndex, out long offsetInSegment)
    {
        segmentIndex = 0;
        offsetInSegment = 0;

        if (_accessor is null || _entryCount == 0)
            return false;

        var idBytes = cardId.ToByteArray();
        var lo = 0;
        var hi = _entryCount - 1;

        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            var entryOffset = HeaderSize + mid * EntrySize;
            var compare = CompareGuidSimd(idBytes, entryOffset);

            if (compare == 0)
            {
                segmentIndex = _accessor.ReadInt32(entryOffset + 16);
                offsetInSegment = _accessor.ReadInt64(entryOffset + 20);
                return true;
            }

            if (compare < 0)
                hi = mid - 1;
            else
                lo = mid + 1;
        }

        return false;
    }

    private int CompareGuidSimd(byte[] target, int entryOffset)
    {
        if (Avx2.IsSupported)
        {
            Span<byte> target32 = stackalloc byte[32];
            target.CopyTo(target32);
            var targetVec = Unsafe.As<byte, Vector256<byte>>(ref target32[0]);

            Span<byte> entry32 = stackalloc byte[32];
            for (var i = 0; i < 16; i++)
                entry32[i] = _accessor!.ReadByte(entryOffset + i);
            var entryVec = Unsafe.As<byte, Vector256<byte>>(ref entry32[0]);

            var cmp = Vector256.Equals(targetVec, entryVec);
            var mask = Avx2.MoveMask(cmp);
            var mask16 = (ushort)(mask & 0xFFFF);
            if (mask16 == 0xFFFF) return 0;
            var firstDiff = BitOperations.TrailingZeroCount(~mask16);
            return target[firstDiff] - entry32[firstDiff];
        }

        if (Sse2.IsSupported)
        {
            var targetVec = Unsafe.As<byte, Vector128<byte>>(ref target[0]);
            Span<byte> entry16 = stackalloc byte[16];
            for (var i = 0; i < 16; i++)
                entry16[i] = _accessor!.ReadByte(entryOffset + i);
            var entryVec = Unsafe.As<byte, Vector128<byte>>(ref entry16[0]);

            var cmp = Sse2.CompareEqual(targetVec, entryVec);
            var mask = (ushort)Sse2.MoveMask(cmp);
            if (mask == 0xFFFF) return 0;
            var firstDiff = BitOperations.TrailingZeroCount(~mask);
            return target[firstDiff] - entry16[firstDiff];
        }

        for (var i = 0; i < 16; i++)
        {
            var b = _accessor!.ReadByte(entryOffset + i);
            var diff = target[i] - b;
            if (diff != 0) return diff;
        }
        return 0;
    }

    public void Upsert(Guid cardId, int segmentIndex, long offsetInSegment)
    {
        lock (_lock)
        {
            if (_accessor is null)
                throw new InvalidOperationException("Index not open");

            var entriesStart = HeaderSize;
            var insertPos = FindInsertPosition(cardId);

            if (insertPos < _entryCount)
            {
                var existingId = ReadGuid(_accessor, entriesStart + (long)insertPos * EntrySize);
                if (existingId == cardId)
                {
                    _accessor.Write(entriesStart + (long)insertPos * EntrySize + 16, segmentIndex);
                    _accessor.Write(entriesStart + (long)insertPos * EntrySize + 20, offsetInSegment);
                    _dirty = true;
                    return;
                }
            }

            var newSize = HeaderSize + (long)(_entryCount + 1) * EntrySize;
            _accessor.Flush();
            _accessor.Dispose();
            _mmf?.Dispose();

            var fs = new FileStream(_indexPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            fs.SetLength(newSize);
            fs.Dispose();

            _mmf = MemoryMappedFile.CreateFromFile(_indexPath, FileMode.Open, null, 0, MemoryMappedFileAccess.ReadWrite);
            _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

            if (_entryCount > 0)
            {
                var shiftStart = entriesStart + (long)insertPos * EntrySize;
                var shiftEnd = entriesStart + (long)_entryCount * EntrySize;
                var shiftLen = (int)(shiftEnd - shiftStart);

                if (shiftLen > 0)
                {
                    var temp = ArrayPool<byte>.Shared.Rent(shiftLen);
                    try
                    {
                        _accessor.ReadArray(shiftStart, temp, 0, shiftLen);
                        _accessor.WriteArray(shiftStart + EntrySize, temp, 0, shiftLen);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(temp);
                    }
                }
            }

            var newEntryOffset = entriesStart + (long)insertPos * EntrySize;
            WriteGuid(_accessor, newEntryOffset, cardId);
            _accessor.Write(newEntryOffset + 16, segmentIndex);
            _accessor.Write(newEntryOffset + 20, offsetInSegment);

            _entryCount++;
            WriteHeader();
            _dirty = true;

            _logger.LogDebug("Index: upserted card {CardId} at segment {Seg}, offset {Off}",
                cardId, segmentIndex, offsetInSegment);
        }
    }

    public void Remove(Guid cardId)
    {
        lock (_lock)
        {
            if (_accessor is null || _entryCount == 0) return;

            var entriesStart = HeaderSize;
            var idx = FindInsertPosition(cardId);

            if (idx >= _entryCount) return;

            var entryId = ReadGuid(_accessor, entriesStart + (long)idx * EntrySize);
            if (entryId != cardId) return;

            if (idx < _entryCount - 1)
            {
                var shiftFrom = entriesStart + (long)(idx + 1) * EntrySize;
                var shiftTo = entriesStart + (long)idx * EntrySize;
                var shiftLen = (_entryCount - idx - 1) * EntrySize;

                var temp = ArrayPool<byte>.Shared.Rent(shiftLen);
                try
                {
                    _accessor.ReadArray(shiftFrom, temp, 0, shiftLen);
                    _accessor.WriteArray(shiftTo, temp, 0, shiftLen);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(temp);
                }
            }

            _entryCount--;
            WriteHeader();
            _dirty = true;
        }
    }

    public void RebuildFromEntries(List<(Guid CardId, int SegmentIndex, long Offset)> entries)
    {
        lock (_lock)
        {
            _accessor?.Dispose();
            _mmf?.Dispose();

            var totalSize = HeaderSize + entries.Count * EntrySize;
            using var fs = new FileStream(_indexPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096);
            fs.SetLength(totalSize);
            fs.Dispose();

            _mmf = MemoryMappedFile.CreateFromFile(_indexPath, FileMode.Open, null, 0, MemoryMappedFileAccess.ReadWrite);
            _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

            var sorted = entries.OrderBy(e => e.CardId).ToList();
            var entriesStart = HeaderSize;

            for (var i = 0; i < sorted.Count; i++)
            {
                var offset = entriesStart + (long)i * EntrySize;
                WriteGuid(_accessor, offset, sorted[i].CardId);
                _accessor.Write(offset + 16, sorted[i].SegmentIndex);
                _accessor.Write(offset + 20, sorted[i].Offset);
            }

            _entryCount = sorted.Count;
            _segmentCount = sorted.Count > 0 ? sorted.Max(e => e.SegmentIndex) + 1 : 0;
            WriteHeader();

            _logger.LogInformation("Index rebuilt: {Entries} entries", _entryCount);
        }
    }

    public byte[] GetEntryBytes(Guid cardId)
    {
        if (_accessor is null) return Array.Empty<byte>();

        var entriesStart = HeaderSize;
        var idx = FindInsertPosition(cardId);

        if (idx >= _entryCount) return Array.Empty<byte>();

        var entryId = ReadGuid(_accessor, entriesStart + (long)idx * EntrySize);
        if (entryId != cardId) return Array.Empty<byte>();

        var buffer = new byte[EntrySize];
        _accessor.ReadArray(entriesStart + (long)idx * EntrySize, buffer, 0, EntrySize);
        return buffer;
    }

    private int FindInsertPosition(Guid cardId)
    {
        var low = 0;
        var high = _entryCount - 1;
        var entriesStart = HeaderSize;

        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            var midId = ReadGuid(_accessor!, entriesStart + (long)mid * EntrySize);
            var cmp = cardId.CompareTo(midId);

            if (cmp == 0) return mid;
            if (cmp < 0) high = mid - 1;
            else low = mid + 1;
        }

        return low;
    }

    public void Flush()
    {
        lock (_lock)
        {
            if (_dirty && _accessor is not null)
            {
                _accessor.Flush();
                _dirty = false;
            }
        }
    }

    private void PrefaultPages()
    {
        try
        {
            if (_accessor is null || _mmf is null) return;

            var length = _accessor.Capacity;
            if (length <= 0) return;

            const int pageSize = 4096;
            var touched = 0;
            for (var offset = 0L; offset < length; offset += pageSize)
            {
                _ = _accessor.ReadByte(offset);
                touched++;
            }

            _logger.LogDebug("Prefaulted {Pages} pages ({Size} KB) of index", touched, length / 1024);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Prefault failed (non-critical)");
        }
    }

    public IEnumerable<(Guid CardId, int SegmentIndex, long Offset)> EnumerateAllEntries()
    {
        lock (_lock)
        {
            if (_accessor is null || _entryCount == 0)
                yield break;

            var entriesStart = HeaderSize;
            for (var i = 0; i < _entryCount; i++)
            {
                var offset = entriesStart + (long)i * EntrySize;
                yield return (
                    ReadGuid(_accessor, offset),
                    _accessor.ReadInt32(offset + 16),
                    _accessor.ReadInt64(offset + 20)
                );
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            Flush();
            _accessor?.Dispose();
            _accessor = null;
            _mmf?.Dispose();
            _mmf = null;
        }
    }
}
