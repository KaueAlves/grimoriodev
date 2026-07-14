using GrimorioDev.Infrastructure.Services;
using Shouldly;

namespace GrimorioDev.Tests;

public sealed class Lz4CompressionServiceTests
{
    private readonly Lz4CompressionService _sut;

    public Lz4CompressionServiceTests()
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<Lz4CompressionService>>();
        _sut = new Lz4CompressionService(logger);
    }

    [Fact]
    public void CompressAndDecompress_Roundtrip()
    {
        var original = "Hello GrimórioDev World!"u8.ToArray();

        var compressed = _sut.CompressToArray(original, CompressionLevel.Fast);
        var decompressed = _sut.DecompressToArray(compressed, original.Length);

        decompressed.ShouldBe(original);
    }

    [Fact]
    public void CompressAndDecompress_EmptyData()
    {
        var original = Array.Empty<byte>();

        var compressed = _sut.CompressToArray(original, CompressionLevel.Fast);
        var decompressed = _sut.DecompressToArray(compressed, original.Length);

        decompressed.ShouldBe(original);
    }

    [Fact]
    public void CompressAndDecompress_LargeData()
    {
        var original = new byte[100000];
        new Random(42).NextBytes(original);

        var compressed = _sut.CompressToArray(original, CompressionLevel.Optimal);

        var decompressed = _sut.DecompressToArray(compressed, original.Length);
        decompressed.ShouldBe(original);
    }

    [Fact]
    public void CompressAndDecompress_HighCompressionLevel()
    {
        var original = "AAAAABBBBBCCCCCDDDDDEEEEEFFFFF"u8.ToArray();

        var fast = _sut.CompressToArray(original, CompressionLevel.Fast);
        var high = _sut.CompressToArray(original, CompressionLevel.HighCompression);

        high.Length.ShouldBeLessThanOrEqualTo(fast.Length);
    }

    [Fact]
    public void Compress_SpanRoundtrip()
    {
        Span<byte> original = "GrimórioDev"u8.ToArray();
        var maxLen = K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(original.Length);
        var compressed = new byte[maxLen];
        var decompressed = new byte[original.Length];

        var compressedSize = _sut.Compress(original, compressed.AsSpan(0, maxLen), CompressionLevel.Fast);
        var decompressedSize = _sut.Decompress(compressed.AsSpan(0, compressedSize), decompressed);

        decompressedSize.ShouldBe(original.Length);
        decompressed.AsSpan().SequenceEqual(original).ShouldBeTrue();
    }
}
