using GrimorioDev.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.UseCases;

public sealed class CompactWal
{
    private readonly WalService _wal;
    private readonly ILogger<CompactWal> _logger;

    public CompactWal(WalService wal, ILogger<CompactWal> logger)
    {
        _wal = wal;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting WAL compaction ({Pending} pending entries)", _wal.PendingEntries);
        await _wal.CompactAsync(cancellationToken);
        _logger.LogInformation("WAL compaction complete");
    }
}
