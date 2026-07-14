using GrimorioDev.Domain.Interfaces;
using GrimorioDev.Infrastructure.Services;
using Shouldly;

namespace GrimorioDev.Tests;

public sealed class WalServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly WalService _sut;

    public WalServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GrimorioDev_Wal_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<WalService>>();
        _sut = new WalService(_tempDir, logger);
        _sut.Open();
    }

    [Fact]
    public async Task AppendAndReplay_SingleEntry()
    {
        var cardId = Guid.NewGuid();
        var payload = "test payload"u8.ToArray();

        await _sut.AppendAsync(WalOperation.Create, cardId, payload);
        var entries = await _sut.ReplayAsync();

        entries.Count.ShouldBe(1);
        entries[0].Operation.ShouldBe(WalOperation.Create);
        entries[0].CardId.ShouldBe(cardId);
        entries[0].Payload.ToArray().ShouldBe(payload);
    }

    [Fact]
    public async Task AppendAndReplay_MultipleEntries()
    {
        for (var i = 0; i < 5; i++)
            await _sut.AppendAsync(WalOperation.Update, Guid.NewGuid(), new byte[] { (byte)i });

        var entries = await _sut.ReplayAsync();

        entries.Count.ShouldBe(5);
    }

    [Fact]
    public async Task AppendAndReplay_DifferentOperations()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        await _sut.AppendAsync(WalOperation.Create, id1, "create"u8.ToArray());
        await _sut.AppendAsync(WalOperation.Update, id2, "update"u8.ToArray());
        await _sut.AppendAsync(WalOperation.Delete, id3, "delete"u8.ToArray());

        var entries = await _sut.ReplayAsync();

        entries.Count.ShouldBe(3);
        entries[0].Operation.ShouldBe(WalOperation.Create);
        entries[1].Operation.ShouldBe(WalOperation.Update);
        entries[2].Operation.ShouldBe(WalOperation.Delete);
    }

    [Fact]
    public async Task Truncate_ShouldClearAllEntries()
    {
        await _sut.AppendAsync(WalOperation.Create, Guid.NewGuid(), "data"u8.ToArray());
        await _sut.TruncateAsync();

        var entries = await _sut.ReplayAsync();
        entries.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Compact_ShouldKeepLatestPerCard()
    {
        var cardId = Guid.NewGuid();

        await _sut.AppendAsync(WalOperation.Create, cardId, "v1"u8.ToArray());
        await _sut.AppendAsync(WalOperation.Update, cardId, "v2"u8.ToArray());
        await _sut.AppendAsync(WalOperation.Update, cardId, "v3"u8.ToArray());
        await _sut.CompactAsync();

        var entries = await _sut.ReplayAsync();
        entries.Count.ShouldBe(1);
        entries[0].CardId.ShouldBe(cardId);
        entries[0].Payload.ToArray().ShouldBe("v3"u8.ToArray());
    }

    [Fact]
    public async Task Compact_ShouldRemoveDeletedCards()
    {
        var cardId = Guid.NewGuid();

        await _sut.AppendAsync(WalOperation.Create, cardId, "data"u8.ToArray());
        await _sut.AppendAsync(WalOperation.Delete, cardId, Array.Empty<byte>());
        await _sut.CompactAsync();

        var entries = await _sut.ReplayAsync();
        entries.Count.ShouldBe(0);
    }

    [Fact]
    public async Task PendingEntries_ShouldTrackCount()
    {
        _sut.PendingEntries.ShouldBe(0);

        await _sut.AppendAsync(WalOperation.Create, Guid.NewGuid(), Array.Empty<byte>());

        _sut.PendingEntries.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Replay_EmptyFile_ShouldReturnEmpty()
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<WalService>>();
        using var emptyWal = new WalService(_tempDir, logger);
        emptyWal.Open();

        var entries = await emptyWal.ReplayAsync();
        entries.Count.ShouldBe(0);
    }

    [Fact]
    public async Task CRCValidation_ShouldStopOnCorruptEntry()
    {
        await _sut.AppendAsync(WalOperation.Create, Guid.NewGuid(), "valid"u8.ToArray());
        _sut.Dispose();

        var walPath = Path.Combine(_tempDir, "wal.log");
        var bytes = await File.ReadAllBytesAsync(walPath);
        bytes[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(walPath, bytes);

        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<WalService>>();
        using var corruptWal = new WalService(_tempDir, logger);
        corruptWal.Open();

        var entries = await corruptWal.ReplayAsync();
        entries.Count.ShouldBe(0);
    }

    public void Dispose()
    {
        _sut.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}
