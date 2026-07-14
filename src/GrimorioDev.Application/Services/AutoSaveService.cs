using Microsoft.Extensions.Logging;

namespace GrimorioDev.Application.Services;

public sealed class AutoSaveService : IDisposable
{
    private readonly DirtyTrackerService _dirtyTracker;
    private readonly WorkspaceService _workspaceService;
    private readonly ILogger<AutoSaveService> _logger;
    private Timer? _timer;
    private bool _disposed;

    public bool IsEnabled { get; set; } = true;
    public int IntervalMs { get; set; } = 30_000;
    public bool IsSaving { get; private set; }

    public AutoSaveService(
        DirtyTrackerService dirtyTracker,
        WorkspaceService workspaceService,
        ILogger<AutoSaveService> logger)
    {
        _dirtyTracker = dirtyTracker;
        _workspaceService = workspaceService;
        _logger = logger;
    }

    public void Start(Guid workspaceId)
    {
        Stop();
        _timer = new Timer(async _ => await AutoSaveCallbackAsync(workspaceId), null, IntervalMs, IntervalMs);
        _logger.LogDebug("Auto-save started with {Interval}ms interval", IntervalMs);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void TriggerImmediate(Guid workspaceId)
    {
        _ = Task.Run(async () => await AutoSaveCallbackAsync(workspaceId));
    }

    private async Task AutoSaveCallbackAsync(Guid workspaceId)
    {
        if (IsSaving || !IsEnabled || !_dirtyTracker.HasPending)
            return;

        IsSaving = true;
        try
        {
            var batch = _dirtyTracker.GetPendingBatch();
            if (batch.Count == 0) return;

            await _workspaceService.SaveWorkspaceAsync(
                new DTOs.SaveWorkspaceRequest(workspaceId, 0, 0));

            foreach (var item in batch)
                _dirtyTracker.MarkSaved(item.CardId);

            _logger.LogDebug("Auto-saved {Count} items", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-save failed");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            _disposed = true;
        }
    }
}
