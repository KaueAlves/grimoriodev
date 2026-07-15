using GrimorioDev.Domain.Entities;

namespace GrimorioDev.Tests;

public sealed class CardTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var pos = new CardPosition(100, 200, 0);
        var card = Card.Create("Test Card", pos, 400, 300);

        card.Title.ShouldBe("Test Card");
        card.Position.ShouldBe(pos);
        card.Width.ShouldBe(400);
        card.Height.ShouldBe(300);
        card.Content.ShouldBeEmpty();
        card.IsPinned.ShouldBeFalse();
        card.Id.ShouldNotBe(Guid.Empty);
        card.CreatedAt.ShouldBeGreaterThan(DateTime.MinValue);
    }

    [Fact]
    public void Create_ShouldThrowOnEmptyTitle()
    {
        Should.Throw<ArgumentException>(() =>
            Card.Create("", new CardPosition(0, 0, 0)));
    }

    [Fact]
    public void Create_ShouldThrowOnWhitespaceTitle()
    {
        Should.Throw<ArgumentException>(() =>
            Card.Create("   ", new CardPosition(0, 0, 0)));
    }

    [Fact]
    public void Create_ShouldUseDefaultSize()
    {
        var card = Card.Create("Default", new CardPosition(10, 20, 1));

        card.Width.ShouldBe(300);
        card.Height.ShouldBe(200);
    }

    [Fact]
    public void UpdateContent_ShouldUpdateAndMarkUpdated()
    {
        var card = Card.Create("Test", new CardPosition(0, 0, 0));
        var originalUpdated = card.UpdatedAt;

        Thread.Sleep(1);
        card.UpdateContent("new content");

        card.Content.ShouldBe("new content");
        card.UpdatedAt.ShouldBeGreaterThan(originalUpdated);
    }

    [Fact]
    public void UpdateTitle_ShouldUpdate()
    {
        var card = Card.Create("Old", new CardPosition(0, 0, 0));

        card.UpdateTitle("New Title");

        card.Title.ShouldBe("New Title");
    }

    [Fact]
    public void UpdateTitle_ShouldThrowOnEmpty()
    {
        var card = Card.Create("Test", new CardPosition(0, 0, 0));

        Should.Throw<ArgumentException>(() => card.UpdateTitle(""));
    }

    [Fact]
    public void MoveTo_ShouldUpdatePosition()
    {
        var card = Card.Create("Test", new CardPosition(0, 0, 0));
        var newPos = new CardPosition(150.5, 300.2, 5);

        card.MoveTo(newPos);

        card.Position.ShouldBe(newPos);
    }

    [Fact]
    public void Resize_ShouldUpdateDimensions()
    {
        var card = Card.Create("Test", new CardPosition(0, 0, 0));

        card.Resize(500, 400);

        card.Width.ShouldBe(500);
        card.Height.ShouldBe(400);
    }

    [Fact]
    public void Resize_ShouldClampMinimumWidth()
    {
        var card = Card.Create("Test", new CardPosition(0, 0, 0));

        card.Resize(10, 200);

        card.Width.ShouldBe(100);
    }

    [Fact]
    public void Resize_ShouldClampMinimumHeight()
    {
        var card = Card.Create("Test", new CardPosition(0, 0, 0));

        card.Resize(300, 5);

        card.Height.ShouldBe(60);
    }

    [Fact]
    public void TogglePin_ShouldToggle()
    {
        var card = Card.Create("Test", new CardPosition(0, 0, 0));

        card.IsPinned.ShouldBeFalse();
        card.TogglePin();
        card.IsPinned.ShouldBeTrue();
        card.TogglePin();
        card.IsPinned.ShouldBeFalse();
    }

    [Fact]
    public void Restore_ShouldRecreateCardExactly()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        var pos = new CardPosition(50, 75, 3);

        var card = Card.Restore(
            id, "Restored", "content here", pos,
            320, 240, true, createdAt, updatedAt);

        card.Id.ShouldBe(id);
        card.Title.ShouldBe("Restored");
        card.Content.ShouldBe("content here");
        card.Position.ShouldBe(pos);
        card.Width.ShouldBe(320);
        card.Height.ShouldBe(240);
        card.IsPinned.ShouldBeTrue();
        card.CreatedAt.ShouldBe(createdAt);
        card.UpdatedAt.ShouldBe(updatedAt);
    }
}
