using System.Collections.Concurrent;

namespace GrimorioDev.Application.Services;

public enum SavePriority
{
    Visible = 0,
    Adjacent = 1,
    OffScreen = 2
}

public sealed class DirtyTrackerService
{
    private readonly ConcurrentDictionary<Guid, SaveRequest> _dirtyItems = new();

    public int PendingCount => _dirtyItems.Count;

    public void MarkDirty(Guid cardId, SavePriority priority = SavePriority.Visible)
    {
        var request = new SaveRequest(cardId, priority, DateTime.UtcNow);
        _dirtyItems.AddOrUpdate(cardId, request, (_, _) => request);
    }

    public void MarkSaved(Guid cardId)
    {
        _dirtyItems.TryRemove(cardId, out _);
    }

    public IReadOnlyList<SaveRequest> GetPendingBatch(int maxCount = 50)
    {
        return _dirtyItems.Values
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Timestamp)
            .Take(maxCount)
            .ToList();
    }

    public bool HasPending => !_dirtyItems.IsEmpty;

    public void Clear()
    {
        _dirtyItems.Clear();
    }
}

public sealed record SaveRequest(
    Guid CardId,
    SavePriority Priority,
    DateTime Timestamp);
