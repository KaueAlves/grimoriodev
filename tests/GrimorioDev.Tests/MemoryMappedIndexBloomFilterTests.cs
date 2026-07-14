using GrimorioDev.Infrastructure.Repositories;
using Shouldly;

namespace GrimorioDev.Tests;

public sealed class MemoryMappedIndexBloomFilterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MemoryMappedIndexBloomFilter _sut;

    public MemoryMappedIndexBloomFilterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GrimorioDev_Bloom_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<MemoryMappedIndexBloomFilter>>();
        _sut = new MemoryMappedIndexBloomFilter(_tempDir, logger, expectedEntries: 10000);
    }

    [Fact]
    public void Add_And_MightContain_ShouldReturnTrue()
    {
        var id = Guid.NewGuid();
        _sut.Add(id);
        _sut.MightContain(id).ShouldBeTrue();
    }

    [Fact]
    public void MightContain_NonExistent_ShouldReturnFalse()
    {
        var result = _sut.MightContain(Guid.NewGuid());
        // Bloom may have false positives, but with 10k bits and few entries, false should be common
        result.ShouldBe(false);
    }

    [Fact]
    public void Add_Multiple_And_CheckAll()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids)
            _sut.Add(id);

        foreach (var id in ids)
            _sut.MightContain(id).ShouldBeTrue($"Bloom should contain {id}");
    }

    [Fact]
    public void FalsePositiveRate_ShouldBeLow()
    {
        var added = Enumerable.Range(0, 500).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in added)
            _sut.Add(id);

        var notAdded = Enumerable.Range(0, 1000).Select(_ => Guid.NewGuid())
            .Where(id => !added.Contains(id)).ToList();

        var falsePositives = notAdded.Count(id => _sut.MightContain(id));

        // False positive rate should be < 5% for 500 entries in 8192-bit filter
        (falsePositives / (double)notAdded.Count).ShouldBeLessThan(0.05);
    }

    [Fact]
    public void Count_ShouldTrackEntries()
    {
        _sut.Count.ShouldBe(0);
        _sut.Add(Guid.NewGuid());
        _sut.Count.ShouldBe(1);
    }

    [Fact]
    public void Clear_ShouldReset()
    {
        _sut.Add(Guid.NewGuid());
        _sut.Clear();

        _sut.Count.ShouldBe(0);
    }

    [Fact]
    public void Rebuild_ShouldReplaceAll()
    {
        var ids = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
        _sut.Add(Guid.NewGuid());

        _sut.Rebuild(ids);

        _sut.Count.ShouldBe(50);
        foreach (var id in ids)
            _sut.MightContain(id).ShouldBeTrue();
    }

    [Fact]
    public void Remove_ShouldNotThrow()
    {
        Should.NotThrow(() => _sut.Remove(Guid.NewGuid()));
    }

    [Fact]
    public void PersistAndReload_ShouldMaintainState()
    {
        var id = Guid.NewGuid();
        _sut.Add(id);
        _sut.Dispose();

        var logger2 = NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger<MemoryMappedIndexBloomFilter>>();
        using var sut2 = new MemoryMappedIndexBloomFilter(_tempDir, logger2, expectedEntries: 10000);

        sut2.MightContain(id).ShouldBeTrue();
        sut2.Count.ShouldBe(1);
    }

    public void Dispose()
    {
        _sut.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}
