using GrimorioDev.Domain.ValueObjects;

namespace GrimorioDev.Domain.Entities;

public sealed class Card : EntityBase
{
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public double Width { get; private set; }
    public double Height { get; private set; }
    public CardPosition Position { get; private set; }
    public bool IsPinned { get; private set; }

    private Card() { }

    public static Card Create(string title, CardPosition position, double width = 300, double height = 200)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Card title cannot be empty.", nameof(title));

        return new Card
        {
            Title = title,
            Content = string.Empty,
            Position = position,
            Width = width,
            Height = height
        };
    }

    public void UpdateContent(string content)
    {
        Content = content;
        MarkUpdated();
    }

    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Card title cannot be empty.", nameof(title));
        Title = title;
        MarkUpdated();
    }

    public void MoveTo(CardPosition newPosition)
    {
        Position = newPosition;
        MarkUpdated();
    }

    public void Resize(double width, double height)
    {
        Width = Math.Max(100, width);
        Height = Math.Max(60, height);
        MarkUpdated();
    }

    public void TogglePin()
    {
        IsPinned = !IsPinned;
        MarkUpdated();
    }

    public static Card Restore(
        Guid id, string title, string content, CardPosition position,
        double width, double height, bool isPinned,
        DateTime createdAt, DateTime updatedAt)
    {
        return new Card
        {
            Id = id,
            Title = title,
            Content = content,
            Position = position,
            Width = width,
            Height = height,
            IsPinned = isPinned,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
