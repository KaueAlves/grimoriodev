using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Services;

public enum CompressionLevel
{
    Fast = 0,
    Optimal = 6,
    HighCompression = 9
}

public sealed class Lz4CompressionService
{
    private readonly ILogger<Lz4CompressionService> _logger;

    public Lz4CompressionService(ILogger<Lz4CompressionService> logger)
    {
        _logger = logger;
    }

    public int Compress(ReadOnlySpan<byte> source, Span<byte> target, CompressionLevel level = CompressionLevel.Fast)
    {
        var lz4Level = level switch
        {
            CompressionLevel.Fast => LZ4Level.L00_FAST,
            CompressionLevel.Optimal => LZ4Level.L06_HC,
            CompressionLevel.HighCompression => LZ4Level.L09_HC,
            _ => LZ4Level.L00_FAST
        };

        return LZ4Codec.Encode(source, target, lz4Level);
    }

    public int Decompress(ReadOnlySpan<byte> source, Span<byte> target)
    {
        return LZ4Codec.Decode(source, target);
    }

    public byte[] CompressToArray(ReadOnlySpan<byte> source, CompressionLevel level = CompressionLevel.Fast)
    {
        var maxLen = LZ4Codec.MaximumOutputSize(source.Length);
        var buffer = new byte[maxLen];
        var written = Compress(source, buffer, level);
        return buffer.AsSpan(0, written).ToArray();
    }

    public byte[] DecompressToArray(ReadOnlySpan<byte> source, int decompressedSize)
    {
        var buffer = new byte[decompressedSize];
        Decompress(source, buffer);
        return buffer;
    }
}
