using System.Threading.Channels;
using GrimorioDev.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Application.Services;

public sealed class AutoSaveService : IDisposable
{
    private readonly DirtyTrackerService _dirtyTracker;
    private readonly WorkspaceService _workspaceService;
    private readonly ILogger<AutoSaveService> _logger;

    private readonly Channel<SaveSignal> _channel = Channel.CreateUnbounded<SaveSignal>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly CancellationTokenSource _cts = new();
    private Task? _consumerTask;
    private Guid _workspaceId;
    private bool _started;
    private bool _disposed;

    private const int MaxBatchSize = 10;
    private const int BatchIntervalMs = 3000;
    private const int CoalesceWindowMs = 3000;

    public bool IsEnabled { get; set; } = true;

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
        if (_started) return;
        _workspaceId = workspaceId;
        _started = true;
        _consumerTask = Task.Run(() => ConsumeAsync(_cts.Token));
        _logger.LogDebug("Auto-save channel started for workspace {Id}", workspaceId);
    }

    public void Stop()
    {
        _cts.Cancel();
        _started = false;
    }

    public void Signal(SavePriority priority = SavePriority.OffScreen)
    {
        if (!_started || !IsEnabled) return;
        _channel.Writer.TryWrite(new SaveSignal(priority, DateTime.UtcNow));
    }

    public void SignalImmediate()
    {
        Signal(SavePriority.Visible);
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var batch = new List<SaveSignal>();
        var coalesceMap = new Dictionary<Guid, DateTime>();
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(BatchIntervalMs));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var reader = _channel.Reader;
                var hasItem = reader.TryRead(out var signal);

                if (!hasItem)
                {
                    // Flush any pending batch when timer ticks
                    if (batch.Count > 0)
                    {
                        await FlushBatchAsync(batch, ct);
                        batch.Clear();
                        coalesceMap.Clear();
                    }

                    try { await reader.WaitToReadAsync(ct); } catch { break; }
                    continue;
                }

                if (signal is null) continue;

                // Coalescing: skip if same card was saved within CoalesceWindowMs
                if (coalesceMap.TryGetValue(signal.CardId, out var lastSignal) &&
                    (DateTime.UtcNow - lastSignal).TotalMilliseconds < CoalesceWindowMs)
                {
                    continue;
                }

                // For Visible priority, flush immediately
                if (signal.Priority == SavePriority.Visible && batch.Count > 0)
                {
                    await FlushBatchAsync(batch, ct);
                    batch.Clear();
                    coalesceMap.Clear();
                }

                // Get pending items from dirty tracker
                var pending = _dirtyTracker.GetPendingBatch(MaxBatchSize);
                if (pending.Count == 0) continue;

                foreach (var item in pending)
                {
                    coalesceMap[item.CardId] = item.Timestamp;
                    batch.Add(new SaveSignal(item.Priority, item.Timestamp) { CardId = item.CardId });
                }

                if (batch.Count >= MaxBatchSize)
                {
                    await FlushBatchAsync(batch, ct);
                    batch.Clear();
                    coalesceMap.Clear();
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // Flush remaining
            if (batch.Count > 0)
                await FlushBatchAsync(batch, CancellationToken.None);
            timer.Dispose();
        }
    }

    private async Task FlushBatchAsync(List<SaveSignal> batch, CancellationToken ct)
    {
        try
        {
            await _workspaceService.SaveWorkspaceAsync(
                new SaveWorkspaceRequest(_workspaceId, 0, 0), ct);

            foreach (var item in batch)
                _dirtyTracker.MarkSaved(item.CardId);

            _logger.LogDebug("Channel auto-saved {Count} items", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Channel auto-save failed");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _channel.Writer.TryComplete();
        _consumerTask?.GetAwaiter().GetResult();
        _cts.Dispose();
    }
}

internal sealed record SaveSignal(SavePriority Priority, DateTime Timestamp)
{
    public Guid CardId { get; init; }
}
