using GrimorioDev.Infrastructure.Services;
using Shouldly;

namespace GrimorioDev.Tests;

public sealed class ContentAddressableStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ContentAddressableStore _sut;
    private readonly byte[] _largeData;

    public ContentAddressableStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GrimorioDev_Dedup_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<ContentAddressableStore>>();
        _sut = new ContentAddressableStore(_tempDir, logger);
        _largeData = new byte[2000];
        new Random(42).NextBytes(_largeData);
    }

    [Fact]
    public async Task ComputeHash_ShouldReturnConsistentHash()
    {
        var (hash1, _) = await _sut.ComputeHashAsync(_largeData);
        var (hash2, _) = await _sut.ComputeHashAsync(_largeData);

        hash1.ShouldBe(hash2);
    }

    [Fact]
    public async Task ComputeHash_DifferentData_ShouldReturnDifferentHash()
    {
        var data1 = new byte[2000];
        var data2 = new byte[2000];
        new Random(1).NextBytes(data1);
        new Random(2).NextBytes(data2);

        var (hash1, _) = await _sut.ComputeHashAsync(data1);
        var (hash2, _) = await _sut.ComputeHashAsync(data2);

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public async Task StoreAndLoadBlob_Roundtrip()
    {
        var (hash, _) = await _sut.ComputeHashAsync(_largeData);

        await _sut.StoreBlobAsync(hash, _largeData);
        var loaded = await _sut.LoadBlobAsync(hash);

        loaded.ToArray().ShouldBe(_largeData);
    }

    [Fact]
    public async Task BlobExists_ShouldReturnTrue_AfterStore()
    {
        var (hash, _) = await _sut.ComputeHashAsync(_largeData);

        await _sut.StoreBlobAsync(hash, _largeData);
        var exists = await _sut.BlobExistsAsync(hash);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task BlobExists_ShouldReturnFalse_ForMissing()
    {
        var hash = new byte[32];
        new Random(1).NextBytes(hash);

        var exists = await _sut.BlobExistsAsync(hash);
        exists.ShouldBeFalse();
    }

    [Fact]
    public void ShouldDedup_SmallData_ShouldReturnFalse()
    {
        _sut.ShouldDedup(100).ShouldBeFalse();
    }

    [Fact]
    public void ShouldDedup_LargeData_ShouldReturnTrue()
    {
        _sut.ShouldDedup(2000).ShouldBeTrue();
    }

    [Fact]
    public void GetBlobPath_ShouldBeDeterministic()
    {
        var hash = new byte[32];
        new Random(1).NextBytes(hash);

        var path1 = _sut.GetBlobPath(hash);
        var path2 = _sut.GetBlobPath(hash);

        path1.ShouldBe(path2);
    }

    [Fact]
    public async Task GetTotalBlobSize_ShouldTrack()
    {
        _sut.GetTotalBlobSize().ShouldBe(0);

        var (hash, _) = await _sut.ComputeHashAsync(_largeData);
        await _sut.StoreBlobAsync(hash, _largeData);

        _sut.GetTotalBlobSize().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetBlobCount_ShouldTrack()
    {
        _sut.GetBlobCount().ShouldBe(0);

        var data1 = new byte[2000];
        var data2 = new byte[2000];
        new Random(1).NextBytes(data1);
        new Random(2).NextBytes(data2);
        var (hash1, _) = await _sut.ComputeHashAsync(data1);
        var (hash2, _) = await _sut.ComputeHashAsync(data2);

        await _sut.StoreBlobAsync(hash1, data1);
        await _sut.StoreBlobAsync(hash2, data2);

        _sut.GetBlobCount().ShouldBe(2);
    }

    [Fact]
    public async Task StoreDuplicate_ShouldNotIncreaseCount()
    {
        var (hash, isNew) = await _sut.ComputeHashAsync(_largeData);
        isNew.ShouldBeTrue();

        await _sut.StoreBlobAsync(hash, _largeData);
        _sut.GetBlobCount().ShouldBe(1);

        var (_, isNew2) = await _sut.ComputeHashAsync(_largeData);
        isNew2.ShouldBeFalse();

        await _sut.StoreBlobAsync(hash, _largeData);
        _sut.GetBlobCount().ShouldBe(1);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}
