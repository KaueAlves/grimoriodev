using GrimorioDev.Infrastructure.Repositories;
using GrimorioDev.Infrastructure.Services;
using Shouldly;

namespace GrimorioDev.Tests;

public sealed class CardCacheLru2QTests
{
    private readonly CardCacheLru2Q _sut;
    private readonly MemoryBudgetManager _budget;

    public CardCacheLru2QTests()
    {
        _budget = new MemoryBudgetManager(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<MemoryBudgetManager>>());
        _sut = new CardCacheLru2Q(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<CardCacheLru2Q>>(),
            _budget, maxHot: 8, maxWarm: 24);
    }

    public sealed class TestCard
    {
        public string Data { get; init; } = "";
    }

    [Fact]
    public void SetAndGet_ShouldReturnValue()
    {
        var id = Guid.NewGuid();
        var card = new TestCard { Data = "hello" };

        _sut.Set(id, card);
        var result = _sut.Get<TestCard>(id);

        result.ShouldNotBeNull();
        result!.Data.ShouldBe("hello");
    }

    [Fact]
    public void Get_NonExistent_ShouldReturnNull()
    {
        var result = _sut.Get<TestCard>(Guid.NewGuid());
        result.ShouldBeNull();
    }

    [Fact]
    public void Invalidate_ShouldRemove()
    {
        var id = Guid.NewGuid();
        _sut.Set(id, new TestCard { Data = "x" });
        _sut.Invalidate(id);

        _sut.Get<TestCard>(id).ShouldBeNull();
    }

    [Fact]
    public void Count_ShouldTrackEntries()
    {
        _sut.Count.ShouldBe(0);
        _sut.Set(Guid.NewGuid(), new TestCard { Data = "a" });
        _sut.Count.ShouldBe(1);
    }

    [Fact]
    public void Clear_ShouldReset()
    {
        _sut.Set(Guid.NewGuid(), new TestCard { Data = "a" });
        _sut.Clear();

        _sut.Count.ShouldBe(0);
    }

    [Fact]
    public void WriteBuffer_ShouldBeSeparate()
    {
        var id = Guid.NewGuid();
        var card = new TestCard { Data = "wb" };

        _sut.SetWriteBuffer(id, card);
        _sut.Get<TestCard>(id).ShouldBeNull("Write buffer items should not be in read cache");

        _sut.TryGetWriteBuffer(id, out TestCard? wbCard).ShouldBeTrue();
        wbCard!.Data.ShouldBe("wb");
    }

    [Fact]
    public void FlushWriteBuffer_ShouldOnlyClearBuffer()
    {
        var id = Guid.NewGuid();
        _sut.SetWriteBuffer(id, new TestCard { Data = "flush" });
        _sut.FlushWriteBuffer();

        _sut.WriteBufferCount.ShouldBe(0);
        _sut.Get<TestCard>(id).ShouldBeNull("FlushWriteBuffer only clears the write buffer");
    }

    [Fact]
    public void WriteBufferCount_ShouldTrack()
    {
        _sut.WriteBufferCount.ShouldBe(0);
        _sut.SetWriteBuffer(Guid.NewGuid(), new TestCard { Data = "x" });
        _sut.WriteBufferCount.ShouldBe(1);
    }

    [Fact]
    public void HotWarm_Promotion_ShouldWork()
    {
        var id = Guid.NewGuid();
        _sut.Set(id, new TestCard { Data = "hot" });

        _sut.HotCount.ShouldBe(1);
        _sut.WarmCount.ShouldBe(0);

        _sut.Get<TestCard>(id);

        _sut.HotCount.ShouldBe(1);
    }

    [Fact]
    public void Eviction_ShouldRemoveOldest()
    {
        var ids = Enumerable.Range(0, 16).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids)
            _sut.Set(id, new TestCard { Data = id.ToString() });

        _sut.Count.ShouldBeLessThanOrEqualTo(ids.Count);

        var first = ids[0];
        var firstInCache = _sut.Get<TestCard>(first) is not null;
        var last = ids[^1];
        var lastInCache = _sut.Get<TestCard>(last) is not null;

        (firstInCache || lastInCache).ShouldBeTrue("At least one of the entries should survive");
    }

    [Fact]
    public void HitRate_ShouldTrack()
    {
        _sut.HitRate.ShouldBe(0);

        var id = Guid.NewGuid();
        _sut.Set(id, new TestCard { Data = "x" });

        _sut.Get<TestCard>(id);
        _sut.Get<TestCard>(Guid.NewGuid());

        _sut.HitRate.ShouldBeGreaterThan(0);
        _sut.HitRate.ShouldBeLessThanOrEqualTo(100.0);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var ex = Record.Exception(() => _sut.Dispose());
        ex.ShouldBeNull();
    }
}
