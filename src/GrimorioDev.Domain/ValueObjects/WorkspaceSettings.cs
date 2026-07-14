namespace GrimorioDev.Domain.ValueObjects;

public sealed class WorkspaceSettings
{
    public int AutoSaveIntervalMs { get; init; } = 30_000;
    public int AutoSaveDebounceMs { get; init; } = 3_000;
    public bool AutoSaveEnabled { get; init; } = true;
    public string Theme { get; init; } = "dark";
    public long MaxAssetSizeBytes { get; init; } = 100 * 1024 * 1024;
    public int CompressionThresholdBytes { get; init; } = 8_192;
    public bool UseCompression { get; init; } = true;
    public bool UseDeduplication { get; init; } = true;
    public bool PreloadAdjacentCards { get; init; } = true;
    public int PreloadRadius { get; init; } = 3;
    public int CacheHotMaxEntries { get; init; } = 128;
    public int CacheWarmMaxEntries { get; init; } = 384;
    public int CacheEvictAfterMs { get; init; } = 30_000;
    public int CacheDecayIntervalMs { get; init; } = 5_000;
    public int MaxConcurrentReads { get; init; } = 4;
    public int WalCompactThresholdEntries { get; init; } = 256;
    public bool WalSyncOnWrite { get; init; } = false;
    public int BackgroundIoMaxBytesPerSec { get; init; } = 50 * 1024 * 1024;
    public int SegmentSizeBytes { get; init; } = 16 * 1024 * 1024;
}
