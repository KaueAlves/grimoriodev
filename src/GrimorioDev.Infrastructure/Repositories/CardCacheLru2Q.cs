using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using GrimorioDev.Domain.Interfaces;
using GrimorioDev.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Repositories;

public sealed class CardCacheLru2Q : ICardCache, IDisposable
{
    private readonly ILogger<CardCacheLru2Q> _logger;
    private readonly MemoryBudgetManager _budget;
    private readonly object _lock = new();

    private readonly LinkedList<CacheEntry> _hotQueue = new();
    private readonly LinkedList<CacheEntry> _warmQueue = new();
    private readonly Dictionary<Guid, LinkedListNode<CacheEntry>> _lookup = new();

    private readonly ConcurrentDictionary<Guid, object> _writeBuffer = new();
    private int _maxHot;
    private int _maxWarm;
    private int _maxWriteBuffer;
    private long _currentMemoryBytes;
    private long _hitCount;
    private long _missCount;
    private DateTime _lastAdaptiveCheck = DateTime.UtcNow;

    private const int DefaultMaxHot = 128;
    private const int DefaultMaxWarm = 384;
    private const int MaxWriteBufferCount = 64;
    private const long MaxWriteBufferBytes = 4 * 1024 * 1024;
    private const int AdaptiveCheckIntervalMs = 30000;
    private const long EstimatedEntrySizeBytes = 2048;

    public int Count { get { lock (_lock) { return _lookup.Count; } } }
    public int HotCount { get { lock (_lock) { return _hotQueue.Count; } } }
    public int WarmCount { get { lock (_lock) { return _warmQueue.Count; } } }
    public int WriteBufferCount => _writeBuffer.Count;
    public double HitRate => (_hitCount + _missCount) > 0 ? (double)_hitCount / (_hitCount + _missCount) * 100 : 0;

    public CardCacheLru2Q(ILogger<CardCacheLru2Q> logger, MemoryBudgetManager budget,
        int maxHot = DefaultMaxHot, int maxWarm = DefaultMaxWarm)
    {
        _logger = logger;
        _budget = budget;
        _maxHot = maxHot;
        _maxWarm = maxWarm;
        _maxWriteBuffer = MaxWriteBufferCount;
    }

    public T? Get<T>(Guid cardId) where T : class
    {
        lock (_lock)
        {
            if (_lookup.TryGetValue(cardId, out var node))
            {
                Interlocked.Increment(ref _hitCount);

                if (node.List == _hotQueue)
                {
                    _hotQueue.Remove(node);
                    _hotQueue.AddFirst(node);
                }
                else if (node.List == _warmQueue)
                {
                    _warmQueue.Remove(node);
                    _hotQueue.AddFirst(node);
                }

                return node.Value.Data as T;
            }

            Interlocked.Increment(ref _missCount);
            return null;
        }
    }

    public void Set<T>(Guid cardId, T card) where T : class
    {
        var estimatedSize = EstimateSize(card);

        lock (_lock)
        {
            if (_lookup.TryGetValue(cardId, out var existingNode))
            {
                _currentMemoryBytes -= existingNode.Value.EstimatedSize;
                existingNode.List?.Remove(existingNode);
                _lookup.Remove(cardId);
            }

            var entry = new CacheEntry(cardId, card, estimatedSize);
            var newNode = _hotQueue.AddFirst(entry);
            _lookup[cardId] = newNode;
            _currentMemoryBytes += estimatedSize;

            EvictIfNeeded();
            MaybeAdaptSizes();
        }
    }

    public void SetWriteBuffer<T>(Guid cardId, T card) where T : class
    {
        if (_writeBuffer.Count >= _maxWriteBuffer)
        {
            FlushWriteBuffer();
        }

        _writeBuffer[cardId] = card;
    }

    public bool TryGetWriteBuffer<T>(Guid cardId, out T? card) where T : class
    {
        if (_writeBuffer.TryGetValue(cardId, out var data) && data is T typed)
        {
            card = typed;
            return true;
        }
        card = null;
        return false;
    }

    public void FlushWriteBuffer()
    {
        _writeBuffer.Clear();
        _logger.LogDebug("Write buffer flushed");
    }

    public void Invalidate(Guid cardId)
    {
        lock (_lock)
        {
            if (_lookup.TryGetValue(cardId, out var node))
            {
                _currentMemoryBytes -= node.Value.EstimatedSize;
                node.List?.Remove(node);
                _lookup.Remove(cardId);
            }

            _writeBuffer.TryRemove(cardId, out _);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _hotQueue.Clear();
            _warmQueue.Clear();
            _lookup.Clear();
            _currentMemoryBytes = 0;
            _writeBuffer.Clear();
        }
    }

    private void EvictIfNeeded()
    {
        while (_hotQueue.Count > _maxHot)
        {
            var last = _hotQueue.Last!;
            _hotQueue.RemoveLast();
            _lookup.Remove(last.Value.CardId);
            _currentMemoryBytes -= last.Value.EstimatedSize;
        }

        while (_warmQueue.Count > _maxWarm)
        {
            var last = _warmQueue.Last!;
            _warmQueue.RemoveLast();
            _lookup.Remove(last.Value.CardId);
            _currentMemoryBytes -= last.Value.EstimatedSize;
        }

        if (_budget.IsCritical)
        {
            var toEvict = _lookup.Count / 2;
            for (var i = 0; i < toEvict && _warmQueue.Count > 0; i++)
            {
                var last = _warmQueue.Last!;
                _warmQueue.RemoveLast();
                _lookup.Remove(last.Value.CardId);
                _currentMemoryBytes -= last.Value.EstimatedSize;
            }
            _logger.LogWarning("Critical memory: evicted {Count} entries", toEvict);
        }
    }

    private void MaybeAdaptSizes()
    {
        if ((DateTime.UtcNow - _lastAdaptiveCheck).TotalMilliseconds < AdaptiveCheckIntervalMs)
            return;

        _lastAdaptiveCheck = DateTime.UtcNow;
        var hitRate = HitRate;

        if (hitRate > 90 && !_budget.IsUnderPressure)
        {
            _maxHot = (int)(_maxHot * 1.25);
            _logger.LogDebug("Adaptive: increased hot queue to {Size}", _maxHot);
        }
        else if (hitRate < 50)
        {
            _maxHot = Math.Max(32, (int)(_maxHot * 0.75));
            _logger.LogDebug("Adaptive: decreased hot queue to {Size}", _maxHot);
        }
    }

    private static long EstimateSize(object data)
    {
        if (data is string s)
            return s.Length * 2 + 64;
        return EstimatedEntrySizeBytes;
    }

    public void Dispose()
    {
        Clear();
    }

    private sealed class CacheEntry
    {
        public Guid CardId { get; }
        public object Data { get; }
        public long EstimatedSize { get; }

        public CacheEntry(Guid cardId, object data, long estimatedSize)
        {
            CardId = cardId;
            Data = data;
            EstimatedSize = estimatedSize;
        }
    }
}
