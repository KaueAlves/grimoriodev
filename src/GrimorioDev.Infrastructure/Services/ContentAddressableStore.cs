using System.Security.Cryptography;
using GrimorioDev.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Services;

public sealed class ContentAddressableStore : IDeduplicationService, IDisposable
{
    private readonly string _blobsPath;
    private readonly ILogger<ContentAddressableStore> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private const int MinDedupSizeBytes = 1024;

    public ContentAddressableStore(string workspacePath, ILogger<ContentAddressableStore> logger)
    {
        _blobsPath = Path.Combine(workspacePath, "blobs");
        _logger = logger;
        Directory.CreateDirectory(_blobsPath);
    }

    public async Task<(byte[] Hash, bool IsNew)> ComputeHashAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        byte[] hash;
        using (var stream = new MemoryStream(data.ToArray(), writable: false))
        {
            hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        var exists = await BlobExistsAsync(hash, cancellationToken).ConfigureAwait(false);
        return (hash, !exists);
    }

    public Task<bool> BlobExistsAsync(byte[] hash, CancellationToken cancellationToken = default)
    {
        var path = GetBlobPath(hash);
        return Task.FromResult(File.Exists(path));
    }

    public async Task StoreBlobAsync(byte[] hash, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (data.Length < MinDedupSizeBytes)
        {
            _logger.LogDebug("Skipping dedup for small blob ({Size} bytes)", data.Length);
            return;
        }

        var path = GetBlobPath(hash);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(path))
            {
                _logger.LogDebug("Blob {Hash} already exists, skipping", Convert.ToHexString(hash.AsSpan(0, 8)));
                return;
            }

            await File.WriteAllBytesAsync(path, data.ToArray(), cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Stored blob {Hash} ({Size} bytes)", Convert.ToHexString(hash.AsSpan(0, 8)), data.Length);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<ReadOnlyMemory<byte>> LoadBlobAsync(byte[] hash, CancellationToken cancellationToken = default)
    {
        var path = GetBlobPath(hash);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Blob {Convert.ToHexString(hash.AsSpan(0, 8))} not found");

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return bytes;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public bool ShouldDedup(int sizeBytes) => sizeBytes >= MinDedupSizeBytes;

    public string GetBlobPath(byte[] hash)
    {
        var hex = Convert.ToHexString(hash);
        var prefix = hex[..16];
        return Path.Combine(_blobsPath, $"{prefix}.blob");
    }

    public long GetTotalBlobSize()
    {
        if (!Directory.Exists(_blobsPath))
            return 0;

        return Directory.GetFiles(_blobsPath, "*.blob")
            .Sum(f => new FileInfo(f).Length);
    }

    public int GetBlobCount()
    {
        if (!Directory.Exists(_blobsPath))
            return 0;

        return Directory.GetFiles(_blobsPath, "*.blob").Length;
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }
}
