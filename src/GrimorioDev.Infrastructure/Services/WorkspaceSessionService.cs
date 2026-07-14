using GrimorioDev.Application.DTOs;
using GrimorioDev.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Services;

public sealed class WorkspaceSessionService : IDisposable
{
    private readonly ILogger<WorkspaceSessionService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lz4CompressionService _compression;
    private readonly MemoryBudgetManager _budgetManager;

    public WorkspaceDto? CurrentWorkspace { get; private set; }
    public DataFileRepository? DataFile { get; private set; }
    public MemoryMappedIndexRepository? Index { get; private set; }
    public MemoryMappedIndexBloomFilter? Bloom { get; private set; }
    public WalService? Wal { get; private set; }
    public ContentAddressableStore? DedupStore { get; private set; }
    public CardCacheLru2Q? Cache { get; private set; }
    public Prefetcher? Prefetcher { get; private set; }

    public bool IsActive => CurrentWorkspace is not null;

    public WorkspaceSessionService(
        ILoggerFactory loggerFactory,
        Lz4CompressionService compression,
        MemoryBudgetManager budgetManager,
        ILogger<WorkspaceSessionService> logger)
    {
        _loggerFactory = loggerFactory;
        _compression = compression;
        _budgetManager = budgetManager;
        _logger = logger;
    }

    public Task InitializeAsync(WorkspaceDto workspace)
    {
        Close();

        var wsPath = workspace.Location;
        Directory.CreateDirectory(wsPath);
        Directory.CreateDirectory(Path.Combine(wsPath, "blobs"));
        Directory.CreateDirectory(Path.Combine(wsPath, "segments"));

        DataFile = new DataFileRepository(
            wsPath, _compression,
            _loggerFactory.CreateLogger<DataFileRepository>());
        DataFile.Open();

        Index = new MemoryMappedIndexRepository(
            wsPath,
            _loggerFactory.CreateLogger<MemoryMappedIndexRepository>());
        Index.Open();

        Bloom = new MemoryMappedIndexBloomFilter(
            wsPath,
            _loggerFactory.CreateLogger<MemoryMappedIndexBloomFilter>());

        Wal = new WalService(
            wsPath,
            _loggerFactory.CreateLogger<WalService>());
        Wal.Open();

        DedupStore = new ContentAddressableStore(
            wsPath,
            _loggerFactory.CreateLogger<ContentAddressableStore>());

        Cache = new CardCacheLru2Q(
            _loggerFactory.CreateLogger<CardCacheLru2Q>(),
            _budgetManager);

        Prefetcher = new Prefetcher(
            DataFile, Index,
            _loggerFactory.CreateLogger<Prefetcher>());

        CurrentWorkspace = workspace;

        _logger.LogInformation(
            "Session initialized for workspace '{Name}' at {Path}",
            workspace.Name, wsPath);

        return Task.CompletedTask;
    }

    public void Close()
    {
        Prefetcher?.Dispose();
        Wal?.Dispose();
        Index?.Dispose();
        DataFile?.Dispose();

        Prefetcher = null;
        Wal = null;
        Index = null;
        DataFile = null;
        Bloom = null;
        DedupStore = null;
        Cache?.Clear();
        Cache = null;

        CurrentWorkspace = null;
    }

    public void Dispose() => Close();
}
