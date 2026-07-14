using GrimorioDev.Domain.ValueObjects;

namespace GrimorioDev.Domain.Entities;

public sealed class Workspace : EntityBase
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public DateTime LastOpenedAt { get; private set; }
    public DateTime LastSavedAt { get; private set; }
    public WorkspaceSettings Settings { get; private set; } = new();
    public int CardCount { get; private set; }
    public long SizeBytes { get; private set; }

    private Workspace() { }

    public static Workspace Create(string name, string location, string description = "", WorkspaceSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workspace name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Workspace location cannot be empty.", nameof(location));

        return new Workspace
        {
            Name = name,
            Location = location,
            Description = description,
            Settings = settings ?? new WorkspaceSettings()
        };
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workspace name cannot be empty.", nameof(name));
        Name = name;
        MarkUpdated();
    }

    public void UpdateDescription(string description)
    {
        Description = description;
        MarkUpdated();
    }

    public void UpdateSettings(WorkspaceSettings settings)
    {
        Settings = settings;
        MarkUpdated();
    }

    public void TouchOpened()
    {
        LastOpenedAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void TouchSaved(int cardCount, long sizeBytes)
    {
        LastSavedAt = DateTime.UtcNow;
        CardCount = cardCount;
        SizeBytes = sizeBytes;
        MarkUpdated();
    }

    public static Workspace Restore(
        Guid id, string name, string location, string description,
        WorkspaceSettings settings, DateTime createdAt, DateTime updatedAt,
        DateTime lastOpenedAt, DateTime lastSavedAt, int cardCount, long sizeBytes)
    {
        return new Workspace
        {
            Id = id,
            Name = name,
            Location = location,
            Description = description,
            Settings = settings,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            LastOpenedAt = lastOpenedAt,
            LastSavedAt = lastSavedAt,
            CardCount = cardCount,
            SizeBytes = sizeBytes
        };
    }
}
