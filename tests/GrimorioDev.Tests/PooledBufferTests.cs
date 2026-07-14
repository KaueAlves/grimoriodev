using GrimorioDev.Infrastructure.IO;
using Shouldly;

namespace GrimorioDev.Tests;

public sealed class PooledBufferTests
{
    [Fact]
    public void Rent_ShouldReturnNonEmptyBuffer()
    {
        using var buf = PooledBuffer.Rent(1024);
        buf.Length.ShouldBe(0);
        buf.Buffer.Length.ShouldBeGreaterThanOrEqualTo(1024);
    }

    [Fact]
    public void Advance_ShouldUpdateLength()
    {
        using var buf = PooledBuffer.Rent(1024);
        buf.Advance(100);
        buf.Length.ShouldBe(100);
    }

    [Fact]
    public void WrittenSpan_ShouldReflectAdvance()
    {
        using var buf = PooledBuffer.Rent(1024);
        var span = buf.GetSpan(10);
        "test"u8.CopyTo(span);
        buf.Advance(4);
        buf.WrittenSpan.Length.ShouldBe(4);
    }

    [Fact]
    public void Reset_ShouldClearLength()
    {
        using var buf = PooledBuffer.Rent(1024);
        buf.Advance(50);
        buf.Reset();
        buf.Length.ShouldBe(0);
    }

    [Fact]
    public void Rent_MultipleBuffers_ShouldWork()
    {
        using var buf1 = PooledBuffer.Rent(64);
        using var buf2 = PooledBuffer.Rent(128);
        using var buf3 = PooledBuffer.Rent(256);

        buf1.Buffer.Length.ShouldBeGreaterThanOrEqualTo(64);
        buf2.Buffer.Length.ShouldBeGreaterThanOrEqualTo(128);
        buf3.Buffer.Length.ShouldBeGreaterThanOrEqualTo(256);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var buf = PooledBuffer.Rent(512);
        var ex = Record.Exception(() => buf.Dispose());
        ex.ShouldBeNull();
    }
}
