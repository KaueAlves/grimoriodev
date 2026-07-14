using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GrimorioDev.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Repositories;

public sealed class MemoryMappedIndexBloomFilter : IBloomFilter, IDisposable
{
    private readonly string _bloomPath;
    private readonly ILogger<MemoryMappedIndexBloomFilter> _logger;
    private byte[] _bits;
    private int _bitCount;
    private int _hashCount;
    private int _entryCount;
    private readonly object _lock = new();

    public int Count => _entryCount;

    public MemoryMappedIndexBloomFilter(string workspacePath, ILogger<MemoryMappedIndexBloomFilter> logger, int expectedEntries = 10000)
    {
        _bloomPath = Path.Combine(workspacePath, "idx.bloom");
        _logger = logger;

        if (expectedEntries <= 2500)
        {
            _bitCount = 1024;
            _hashCount = 3;
        }
        else if (expectedEntries <= 50000)
        {
            _bitCount = 8192;
            _hashCount = 5;
        }
        else
        {
            _bitCount = 65536;
            _hashCount = 7;
        }

        _bits = new byte[_bitCount / 8];

        if (File.Exists(_bloomPath))
            Load();
    }

    public void Add(Guid cardId)
    {
        lock (_lock)
        {
            var span = cardId.ToByteArray();
            for (var i = 0; i < _hashCount; i++)
            {
                var bitIndex = GetBitIndex(span, i);
                var byteIndex = bitIndex / 8;
                var bitOffset = bitIndex % 8;
                _bits[byteIndex] |= (byte)(1 << bitOffset);
            }
            _entryCount++;
            Save();
        }
    }

    public bool MightContain(Guid cardId)
    {
        var span = cardId.ToByteArray();

        if (Vector.IsHardwareAccelerated && _bits.Length >= Vector<byte>.Count)
        {
            return MightContainSimd(span);
        }

        return MightContainScalar(span);
    }

    private bool MightContainSimd(ReadOnlySpan<byte> id)
    {
        for (var i = 0; i < _hashCount; i++)
        {
            var bitIndex = GetBitIndex(id, i);
            var byteIndex = bitIndex / 8;
            var bitOffset = bitIndex % 8;

            var mask = (byte)(1 << bitOffset);
            if ((_bits[byteIndex] & mask) == 0)
                return false;
        }
        return true;
    }

    private bool MightContainScalar(ReadOnlySpan<byte> id)
    {
        for (var i = 0; i < _hashCount; i++)
        {
            var bitIndex = GetBitIndex(id, i);
            var byteIndex = bitIndex / 8;
            var bitOffset = bitIndex % 8;

            if ((_bits[byteIndex] & (byte)(1 << bitOffset)) == 0)
                return false;
        }
        return true;
    }

    public void Remove(Guid cardId)
    {
        // Bloom filters don't support accurate removal.
        // We skip removal — entries will be re-added on rebuild.
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_bits);
            _entryCount = 0;
            Save();
        }
    }

    public void Rebuild(IEnumerable<Guid> allCardIds)
    {
        lock (_lock)
        {
            Array.Clear(_bits);
            _entryCount = 0;

            foreach (var id in allCardIds)
            {
                var span = id.ToByteArray();
                for (var i = 0; i < _hashCount; i++)
                {
                    var bitIndex = GetBitIndex(span, i);
                    _bits[bitIndex / 8] |= (byte)(1 << (bitIndex % 8));
                }
                _entryCount++;
            }

            Save();
            _logger.LogDebug("Bloom filter rebuilt: {Count} entries, {Bits} bits", _entryCount, _bitCount);
        }
    }

    private int GetBitIndex(ReadOnlySpan<byte> data, int hashSeed)
    {
        uint hash;
        if (hashSeed == 0)
        {
            hash = Fnv1a(data);
        }
        else if (hashSeed == 1)
        {
            hash = Murmur3(data, 1);
        }
        else
        {
            hash = Murmur3(data, (uint)hashSeed);
        }
        return (int)(hash % (uint)_bitCount);
    }

    private static uint Fnv1a(ReadOnlySpan<byte> data)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;
        foreach (var b in data)
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }

    private static uint Murmur3(ReadOnlySpan<byte> data, uint seed)
    {
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;

        var length = data.Length;
        var h1 = seed;
        var remaining = length;
        var index = 0;
        var k1 = 0u;

        while (remaining >= 4)
        {
            k1 = BitConverter.ToUInt32(data[index..]);
            k1 *= c1;
            k1 = BitOperations.RotateLeft(k1, 15);
            k1 *= c2;
            h1 ^= k1;
            h1 = BitOperations.RotateLeft(h1, 13);
            h1 = h1 * 5 + 0xe6546b64;
            index += 4;
            remaining -= 4;
        }

        k1 = 0u;
        switch (remaining)
        {
            case 3: k1 ^= (uint)data[index + 2] << 16; goto case 2;
            case 2: k1 ^= (uint)data[index + 1] << 8; goto case 1;
            case 1:
                k1 ^= data[index];
                k1 *= c1;
                k1 = BitOperations.RotateLeft(k1, 15);
                k1 *= c2;
                h1 ^= k1;
                break;
        }

        h1 ^= (uint)length;
        h1 ^= h1 >> 16;
        h1 *= 0x85ebca6b;
        h1 ^= h1 >> 13;
        h1 *= 0xc2b2ae35;
        h1 ^= h1 >> 16;

        return h1;
    }

    private void Save()
    {
        try
        {
            using var fs = new FileStream(_bloomPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var header = BitConverter.GetBytes(_bitCount);
            var countBytes = BitConverter.GetBytes(_entryCount);
            fs.Write(header, 0, 4);
            fs.Write(countBytes, 0, 4);
            fs.Write(_bits, 0, _bits.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save bloom filter");
        }
    }

    private void Load()
    {
        try
        {
            var bytes = File.ReadAllBytes(_bloomPath);
            if (bytes.Length < 8) return;

            _bitCount = BitConverter.ToInt32(bytes, 0);
            _entryCount = BitConverter.ToInt32(bytes, 4);
            _bits = new byte[_bitCount / 8];
            Array.Copy(bytes, 8, _bits, 0, Math.Min(_bits.Length, bytes.Length - 8));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load bloom filter");
        }
    }

    public void Dispose()
    {
        Save();
    }
}
