using GrimorioDev.Infrastructure.Repositories;
using Shouldly;

namespace GrimorioDev.Tests;

public sealed class MemoryMappedIndexRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MemoryMappedIndexRepository _sut;

    public MemoryMappedIndexRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GrimorioDev_Index_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<MemoryMappedIndexRepository>>();
        _sut = new MemoryMappedIndexRepository(_tempDir, logger);
        _sut.Open();
    }

    [Fact]
    public void UpsertAndFind_Roundtrip()
    {
        var id = Guid.NewGuid();
        _sut.Upsert(id, 1, 12345);

        _sut.TryFind(id, out var segment, out var offset).ShouldBeTrue();
        segment.ShouldBe(1);
        offset.ShouldBe(12345L);
    }

    [Fact]
    public void TryFind_NonExistent_ShouldReturnFalse()
    {
        _sut.TryFind(Guid.NewGuid(), out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void Upsert_MultipleEntries_ShouldBeSorted()
    {
        var ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).OrderBy(_ => Random.Shared.Next()).ToList();

        // Store original insertion index for each Guid
        var insertionOrder = new Dictionary<Guid, int>();
        for (var i = 0; i < ids.Count; i++)
        {
            _sut.Upsert(ids[i], i, i * 1000);
            insertionOrder[ids[i]] = i;
        }

        ids.Sort();

        for (var i = 0; i < ids.Count; i++)
        {
            _sut.TryFind(ids[i], out var seg, out var off).ShouldBeTrue();
            seg.ShouldBe(insertionOrder[ids[i]], $"Guid {ids[i]} should have segment index from its insertion position");
        }

        _sut.EntryCount.ShouldBe(ids.Count);
    }

    [Fact]
    public void Upsert_UpdateExisting_ShouldOverwrite()
    {
        var id = Guid.NewGuid();
        _sut.Upsert(id, 1, 100);
        _sut.Upsert(id, 2, 200);

        _sut.TryFind(id, out var seg, out var off);
        seg.ShouldBe(2);
        off.ShouldBe(200L);
        _sut.EntryCount.ShouldBe(1);
    }

    [Fact]
    public void Remove_ShouldDeleteEntry()
    {
        var id = Guid.NewGuid();
        _sut.Upsert(id, 0, 0);
        _sut.Remove(id);

        _sut.TryFind(id, out _, out _).ShouldBeFalse();
        _sut.EntryCount.ShouldBe(0);
    }

    [Fact]
    public void Remove_NonExistent_ShouldNotThrow()
    {
        Should.NotThrow(() => _sut.Remove(Guid.NewGuid()));
    }

    [Fact]
    public void RebuildFromEntries_ShouldReplaceAll()
    {
        _sut.Upsert(Guid.NewGuid(), 0, 0);

        var entries = new List<(Guid CardId, int SegmentIndex, long Offset)>
        {
            (Guid.NewGuid(), 1, 100),
            (Guid.NewGuid(), 2, 200)
        };

        _sut.RebuildFromEntries(entries);
        _sut.EntryCount.ShouldBe(2);
    }

    [Fact]
    public void GetEntryBytes_ShouldReturnBytes()
    {
        var id = Guid.NewGuid();
        _sut.Upsert(id, 5, 999);

        var bytes = _sut.GetEntryBytes(id);
        bytes.Length.ShouldBe(MemoryMappedIndexRepository.EntrySize);
    }

    [Fact]
    public void GetEntryBytes_NonExistent_ShouldReturnEmpty()
    {
        var bytes = _sut.GetEntryBytes(Guid.NewGuid());
        bytes.ShouldBeEmpty();
    }

    [Fact]
    public void FlushAndReopen_ShouldPersist()
    {
        var id = Guid.NewGuid();
        _sut.Upsert(id, 3, 456);
        _sut.Flush();
        _sut.Dispose();

        var logger2 = NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger<MemoryMappedIndexRepository>>();
        using var sut2 = new MemoryMappedIndexRepository(_tempDir, logger2);
        sut2.Open();

        sut2.TryFind(id, out var seg, out var off).ShouldBeTrue();
        seg.ShouldBe(3);
        off.ShouldBe(456L);
        sut2.EntryCount.ShouldBe(1);
    }

    public void Dispose()
    {
        _sut.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}
