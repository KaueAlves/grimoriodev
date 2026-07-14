using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using GrimorioDev.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Services;

public sealed class WalService : IWalService, IDisposable
{
    private readonly string _walPath;
    private readonly ILogger<WalService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly List<WalPendingEntry> _pendingBuffer = new();
    private FileStream? _walStream;
    private int _pendingCount;
    private DateTime _lastFlush = DateTime.UtcNow;
    private Timer? _flushTimer;

    private const uint Magic = 0x57414C00;
    private const int CurrentVersion = 1;
    private const int HeaderSize = 16;
    private const int EntryHeaderSize = 29;
    private const int MaxBatchSize = 10;
    private const int MaxBatchIntervalMs = 3000;
    private const int MaxPendingEntries = 256;

    public int PendingEntries => _pendingCount;

    public WalService(string workspacePath, ILogger<WalService> logger)
    {
        _walPath = Path.Combine(workspacePath, "wal.log");
        _logger = logger;
    }

    public void Open()
    {
        if (_walStream is not null) return;

        var exists = File.Exists(_walPath);
        _walStream = new FileStream(
            _walPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        if (!exists || _walStream.Length == 0)
        {
            WriteWalHeader();
        }

        _flushTimer = new Timer(async _ => await FlushPendingAsync(), null, MaxBatchIntervalMs, MaxBatchIntervalMs);
    }

    public async Task AppendAsync(WalOperation operation, Guid cardId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var entry = new WalPendingEntry
        {
            Operation = operation,
            CardId = cardId,
            Timestamp = DateTime.UtcNow,
            Payload = payload.ToArray()
        };

        lock (_pendingBuffer)
        {
            _pendingBuffer.Add(entry);
            _pendingCount++;
        }

        if (_pendingCount >= MaxBatchSize ||
            (DateTime.UtcNow - _lastFlush).TotalMilliseconds >= MaxBatchIntervalMs)
        {
            await FlushPendingAsync();
        }
    }

    private async Task FlushPendingAsync()
    {
        List<WalPendingEntry> batch;
        lock (_pendingBuffer)
        {
            if (_pendingBuffer.Count == 0) return;
            batch = new List<WalPendingEntry>(_pendingBuffer);
            _pendingBuffer.Clear();
            _pendingCount = 0;
        }

        await _writeLock.WaitAsync();
        try
        {
            if (_walStream is null) return;

            foreach (var entry in batch)
            {
                await WriteEntryAsync(entry);
            }

            await _walStream.FlushAsync();
            _lastFlush = DateTime.UtcNow;

            _logger.LogDebug("WAL flushed {Count} entries", batch.Count);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task WriteEntryAsync(WalPendingEntry entry)
    {
        if (_walStream is null) return;

        var payloadBytes = entry.Payload;
        var entrySize = EntryHeaderSize + payloadBytes.Length + 4;

        var buffer = ArrayPool<byte>.Shared.Rent(entrySize);
        try
        {
            var offset = 0;

            buffer[offset++] = (byte)entry.Operation;
            entry.CardId.TryWriteBytes(buffer.AsSpan(offset, 16));
            offset += 16;

            BitConverter.GetBytes(entry.Timestamp.Ticks).CopyTo(buffer, offset);
            offset += 8;

            BitConverter.GetBytes(payloadBytes.Length).CopyTo(buffer, offset);
            offset += 4;

            payloadBytes.CopyTo(buffer.AsSpan(offset, payloadBytes.Length));
            offset += payloadBytes.Length;

            var crc = ComputeCrc32(buffer.AsSpan(0, offset));
            BitConverter.GetBytes(crc).CopyTo(buffer, offset);
            offset += 4;

            await _walStream!.WriteAsync(buffer.AsMemory(0, offset));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task<IReadOnlyList<WalEntry>> ReplayAsync(CancellationToken cancellationToken = default)
    {
        var entries = new List<WalEntry>();

        if (!File.Exists(_walPath))
            return entries;

        await using var fs = new FileStream(_walPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fs.Length <= HeaderSize)
            return entries;

        fs.Seek(HeaderSize, SeekOrigin.Begin);

        while (fs.Position < fs.Length)
        {
            var startPos = fs.Position;
            if (fs.Length - startPos < EntryHeaderSize) break;

            var headerBuf = new byte[EntryHeaderSize];
            await fs.ReadAsync(headerBuf, cancellationToken);

            var operation = (WalOperation)headerBuf[0];
            var cardId = new Guid(headerBuf.AsSpan(1, 16));
            var timestampTicks = MemoryMarshal.Read<long>(headerBuf.AsSpan(17, 8));
            var payloadSize = MemoryMarshal.Read<int>(headerBuf.AsSpan(25, 4));

            if (payloadSize < 0 || payloadSize > 10 * 1024 * 1024)
            {
                _logger.LogWarning("WAL: invalid payload size {Size} at offset {Offset}", payloadSize, startPos);
                break;
            }

            var payload = new byte[payloadSize];
            await fs.ReadAsync(payload, cancellationToken);

            var crcBuf = new byte[4];
            await fs.ReadAsync(crcBuf, cancellationToken);
            var storedCrc = BitConverter.ToUInt32(crcBuf);

            var crcData = new byte[EntryHeaderSize + payloadSize];
            Buffer.BlockCopy(headerBuf, 0, crcData, 0, EntryHeaderSize);
            Buffer.BlockCopy(payload, 0, crcData, EntryHeaderSize, payloadSize);
            var computedCrc = ComputeCrc32(crcData);

            if (storedCrc != computedCrc)
            {
                _logger.LogWarning("WAL: CRC mismatch at offset {Offset}, stopping replay", startPos);
                break;
            }

            entries.Add(new WalEntry
            {
                Operation = operation,
                CardId = cardId,
                Timestamp = new DateTime(timestampTicks, DateTimeKind.Utc),
                Payload = payload
            });
        }

        _logger.LogInformation("WAL replayed {Count} entries", entries.Count);
        return entries;
    }

    public async Task TruncateAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (_walStream is not null)
            {
                _walStream.SetLength(0);
                WriteWalHeader();
                await _walStream.FlushAsync(cancellationToken);
            }

            lock (_pendingBuffer)
            {
                _pendingBuffer.Clear();
                _pendingCount = 0;
            }

            _logger.LogDebug("WAL truncated");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task CompactAsync(CancellationToken cancellationToken = default)
    {
        var entries = await ReplayAsync(cancellationToken);
        var latestByCard = new Dictionary<Guid, WalEntry>();

        foreach (var entry in entries)
        {
            if (entry.Operation == WalOperation.Delete)
                latestByCard.Remove(entry.CardId);
            else
                latestByCard[entry.CardId] = entry;
        }

        await TruncateAsync(cancellationToken);

        foreach (var entry in latestByCard.Values)
        {
            await AppendAsync(entry.Operation, entry.CardId, entry.Payload, cancellationToken);
        }

        await FlushPendingAsync();
        _logger.LogInformation("WAL compacted: {Entries} entries", latestByCard.Count);
    }

    private void WriteWalHeader()
    {
        if (_walStream is null) return;

        Span<byte> header = stackalloc byte[HeaderSize];
        BitConverter.GetBytes(Magic).CopyTo(header);
        BitConverter.GetBytes(CurrentVersion).CopyTo(header[4..]);
        BitConverter.GetBytes(0).CopyTo(header[8..]);
        BitConverter.GetBytes(0u).CopyTo(header[12..]);

        _walStream.Seek(0, SeekOrigin.Begin);
        _walStream.Write(header);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320 & (uint)(-(int)(crc & 1)));
        }
        return ~crc;
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        _walStream?.Dispose();
        _walStream = null;
    }

    private sealed class WalPendingEntry
    {
        public WalOperation Operation { get; init; }
        public Guid CardId { get; init; }
        public DateTime Timestamp { get; init; }
        public byte[] Payload { get; init; } = Array.Empty<byte>();
    }
}
