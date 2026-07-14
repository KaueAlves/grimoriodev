using GrimorioDev.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Services;

public sealed class Prefetcher : IDisposable
{
    private readonly DataFileRepository _dataRepository;
    private readonly MemoryMappedIndexRepository _indexRepository;
    private readonly ILogger<Prefetcher> _logger;
    private readonly int _radius;
    private CancellationTokenSource? _cts;

    public Prefetcher(
        DataFileRepository dataRepository,
        MemoryMappedIndexRepository indexRepository,
        ILogger<Prefetcher> logger,
        int radius = 3)
    {
        _dataRepository = dataRepository;
        _indexRepository = indexRepository;
        _logger = logger;
        _radius = radius;
    }

    public void PrefetchAdjacentSegments(int currentSegment)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            try
            {
                var start = Math.Max(0, currentSegment - _radius);
                var end = currentSegment + _radius;

                for (var seg = start; seg <= end; seg++)
                {
                    if (token.IsCancellationRequested) break;

                    var offset = _dataRepository.GetSegmentStartOffset(seg);
                    _logger.LogTrace("Prefetching segment {Segment} at offset {Offset}", seg, offset);

                    await Task.Delay(1, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Prefetch failed");
            }
        }, token);
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _cts = null;
    }
}
