using GrimorioDev.Domain.Interfaces;
using GrimorioDev.Infrastructure.Repositories;
using GrimorioDev.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.UseCases;

public sealed class LoadCard
{
    private readonly MemoryMappedIndexRepository _index;
    private readonly DataFileRepository _dataFile;
    private readonly ContentAddressableStore _dedupStore;
    private readonly CardCacheLru2Q _cache;
    private readonly MemoryMappedIndexBloomFilter _bloom;
    private readonly Prefetcher _prefetcher;
    private readonly ILogger<LoadCard> _logger;

    public LoadCard(
        MemoryMappedIndexRepository index,
        DataFileRepository dataFile,
        ContentAddressableStore dedupStore,
        CardCacheLru2Q cache,
        MemoryMappedIndexBloomFilter bloom,
        Prefetcher prefetcher,
        ILogger<LoadCard> logger)
    {
        _index = index;
        _dataFile = dataFile;
        _dedupStore = dedupStore;
        _cache = cache;
        _bloom = bloom;
        _prefetcher = prefetcher;
        _logger = logger;
    }

    public async Task<byte[]> ExecuteAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        var cached = _cache.Get<byte[]>(cardId);
        if (cached is not null)
        {
            _logger.LogTrace("Cache hit for card {CardId}", cardId);
            return cached;
        }

        if (!_bloom.MightContain(cardId))
        {
            _logger.LogTrace("Bloom filter: card {CardId} definitely not present", cardId);
            throw new KeyNotFoundException($"Card {cardId} not found (bloom filter negative)");
        }

        if (!_index.TryFind(cardId, out var segmentIndex, out var offsetInSegment))
        {
            _logger.LogWarning("Card {CardId} passed bloom but not in index", cardId);
            throw new KeyNotFoundException($"Card {cardId} not found in index");
        }

        _prefetcher.PrefetchAdjacentSegments(segmentIndex);

        try
        {
            var data = await _dataFile.ReadEntryAsync(segmentIndex, offsetInSegment);
            var bytes = data.ToArray();
            _cache.Set(cardId, bytes);
            return bytes;
        }
        catch (DedupReferenceException dre)
        {
            _logger.LogDebug("Card {CardId} is dedup reference", cardId);
            var blob = await _dedupStore.LoadBlobAsync(dre.ContentHash, cancellationToken);
            var bytes = blob.ToArray();
            _cache.Set(cardId, bytes);
            return bytes;
        }
    }
}
