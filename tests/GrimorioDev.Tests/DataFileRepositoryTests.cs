using GrimorioDev.Domain.Entities;
using GrimorioDev.Infrastructure.Repositories;
using GrimorioDev.Infrastructure.Services;
using Shouldly;

namespace GrimorioDev.Tests;

public sealed class DataFileRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DataFileRepository _sut;
    private readonly Lz4CompressionService _compression;

    public DataFileRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GrimorioDev_DataFile_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<DataFileRepository>>();
        _compression = new Lz4CompressionService(Substitute.For<Microsoft.Extensions.Logging.ILogger<Lz4CompressionService>>());
        _sut = new DataFileRepository(_tempDir, _compression, logger, segmentSizeBytes: 1024 * 1024);
        _sut.Open();
    }

    [Fact]
    public async Task AppendAndRead_Roundtrip()
    {
        var cardId = Guid.NewGuid();
        var payload = "Hello DataFile!"u8.ToArray();

        var (segment, offset) = await _sut.AppendAsync(cardId, payload, compress: false);
        var result = await _sut.ReadEntryAsync(segment, offset);

        result.ToArray().ShouldBe(payload);
    }

    [Fact]
    public async Task AppendAndRead_WithCompression()
    {
        var cardId = Guid.NewGuid();
        var payload = new byte[10000];
        new Random(42).NextBytes(payload);

        var (segment, offset) = await _sut.AppendAsync(cardId, payload, compress: true);
        var result = await _sut.ReadEntryAsync(segment, offset);

        result.ToArray().ShouldBe(payload);
    }

    [Fact]
    public async Task AppendMultipleEntries_ShouldReadCorrectly()
    {
        var payloads = new[] { "First"u8.ToArray(), "Second"u8.ToArray(), "Third"u8.ToArray() };
        var offsets = new (int Segment, long Offset)[payloads.Length];

        for (var i = 0; i < payloads.Length; i++)
            offsets[i] = await _sut.AppendAsync(Guid.NewGuid(), payloads[i], compress: false);

        for (var i = 0; i < payloads.Length; i++)
        {
            var result = await _sut.ReadEntryAsync(offsets[i].Segment, offsets[i].Offset);
            result.ToArray().ShouldBe(payloads[i]);
        }
    }

    [Fact]
    public async Task AppendDedupPointer_ShouldThrowOnRead()
    {
        var hash = new byte[32];
        new Random(1).NextBytes(hash);

        await _sut.AppendDedupPointerAsync(hash[..16]);

        var ex = await Should.ThrowAsync<DedupReferenceException>(async () =>
            await _sut.ReadEntryAsync(0, _sut.CurrentOffset - 24));

        ex.ContentHash.ShouldBe(hash[..16]);
    }

    [Fact]
    public async Task CurrentOffset_ShouldIncreaseAfterWrite()
    {
        var before = _sut.CurrentOffset;

        await _sut.AppendAsync(Guid.NewGuid(), "test"u8.ToArray(), compress: false);

        _sut.CurrentOffset.ShouldBeGreaterThan(before);
    }

    public void Dispose()
    {
        _sut.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}
