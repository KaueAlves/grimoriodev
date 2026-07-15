using GrimorioDev.Domain.Entities;

namespace GrimorioDev.Tests;

public sealed class CardPositionTests
{
    [Fact]
    public void Offset_ShouldReturnNewPosition()
    {
        var pos = new CardPosition(100, 200, 0);

        var result = pos.Offset(50, -25);

        result.X.ShouldBe(150);
        result.Y.ShouldBe(175);
        result.ZIndex.ShouldBe(0);
    }

    [Fact]
    public void Offset_ShouldNotMutateOriginal()
    {
        var pos = new CardPosition(100, 200, 0);

        pos.Offset(50, 50);

        pos.X.ShouldBe(100);
        pos.Y.ShouldBe(200);
    }

    [Fact]
    public void Equality_ShouldBeByValue()
    {
        var a = new CardPosition(10, 20, 5);
        var b = new CardPosition(10, 20, 5);
        var c = new CardPosition(99, 20, 5);

        a.Equals(b).ShouldBeTrue();
        a.Equals(c).ShouldBeFalse();
    }

    [Fact]
    public void Deconstruct_ShouldWork()
    {
        var pos = new CardPosition(1.5, 2.5, 3);

        var (x, y, z) = pos;

        x.ShouldBe(1.5);
        y.ShouldBe(2.5);
        z.ShouldBe(3);
    }

    [Fact]
    public void DefaultZIndex_ShouldBeZero()
    {
        var pos = new CardPosition(0, 0, 0);

        pos.ZIndex.ShouldBe(0);
    }
}
