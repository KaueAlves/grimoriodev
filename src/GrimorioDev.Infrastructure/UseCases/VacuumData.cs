using GrimorioDev.Infrastructure.Repositories;
using GrimorioDev.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.UseCases;

public sealed class VacuumData
{
    private readonly DataFileRepository _dataFile;
    private readonly MemoryMappedIndexRepository _index;
    private readonly MemoryMappedIndexBloomFilter _bloom;
    private readonly Lz4CompressionService _compression;
    private readonly ILogger<VacuumData> _logger;

    public VacuumData(
        DataFileRepository dataFile,
        MemoryMappedIndexRepository index,
        MemoryMappedIndexBloomFilter bloom,
        Lz4CompressionService compression,
        ILogger<VacuumData> logger)
    {
        _dataFile = dataFile;
        _index = index;
        _bloom = bloom;
        _compression = compression;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting data vacuum");

        var workspacePath = Path.GetDirectoryName(_dataFile.GetType().Assembly.Location);
        var tempPath = workspacePath + ".vacuum.tmp";

        var entries = new List<(Guid CardId, int SegmentIndex, long Offset)>();

        try
        {
            using var tempDataFile = new DataFileRepository(
                Path.GetDirectoryName(tempPath)!,
                _compression,
                _logger as ILogger<DataFileRepository> ?? throw new InvalidOperationException());

            tempDataFile.Open();

            var allIds = GetAllCardIds();

            foreach (var cardId in allIds)
            {
                if (!_index.TryFind(cardId, out var seg, out var off))
                    continue;

                try
                {
                    var data = await _dataFile.ReadEntryAsync(seg, off).ConfigureAwait(false);
                    var writeResult = await tempDataFile.AppendAsync(
                        cardId, data, compress: true).ConfigureAwait(false);
                    entries.Add((cardId, writeResult.SegmentIndex, writeResult.Offset));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read card {CardId} during vacuum", cardId);
                }
            }

            _index.RebuildFromEntries(entries);
            _bloom.Rebuild(entries.Select(e => e.CardId));

            _logger.LogInformation("Vacuum complete: {Count} entries rewritten", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vacuum failed");
            throw;
        }
    }

    private IEnumerable<Guid> GetAllCardIds()
    {
        var count = _index.EntryCount;
        for (var i = 0; i < count; i++)
        {
            yield return Guid.NewGuid();
        }
    }
}
