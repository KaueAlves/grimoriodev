using GrimorioDev.Domain.Interfaces;
using GrimorioDev.Infrastructure.Repositories;
using GrimorioDev.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.UseCases;

public sealed class RecoverWorkspace
{
    private readonly WalService _wal;
    private readonly DataFileRepository _dataFile;
    private readonly MemoryMappedIndexRepository _index;
    private readonly MemoryMappedIndexBloomFilter _bloom;
    private readonly CardCacheLru2Q _cache;
    private readonly ILogger<RecoverWorkspace> _logger;

    public RecoverWorkspace(
        WalService wal,
        DataFileRepository dataFile,
        MemoryMappedIndexRepository index,
        MemoryMappedIndexBloomFilter bloom,
        CardCacheLru2Q cache,
        ILogger<RecoverWorkspace> logger)
    {
        _wal = wal;
        _dataFile = dataFile;
        _index = index;
        _bloom = bloom;
        _cache = cache;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _wal.ReplayAsync(cancellationToken);
        if (entries.Count == 0)
        {
            _logger.LogInformation("No WAL entries to replay");
            return 0;
        }

        var appliedCount = 0;

        foreach (var entry in entries)
        {
            try
            {
                switch (entry.Operation)
                {
                    case WalOperation.Create:
                    case WalOperation.Update:
                        var result = await _dataFile.AppendAsync(
                            entry.CardId, entry.Payload, compress: false);
                        _index.Upsert(entry.CardId, result.SegmentIndex, result.Offset);
                        _bloom.Add(entry.CardId);
                        appliedCount++;
                        break;

                    case WalOperation.Delete:
                        _index.Remove(entry.CardId);
                        appliedCount++;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replay WAL entry for card {CardId}", entry.CardId);
            }
        }

        _cache.Clear();
        await _wal.TruncateAsync(cancellationToken);

        _logger.LogInformation("WAL recovery complete: {Applied}/{Total} entries applied", appliedCount, entries.Count);
        return appliedCount;
    }
}
