using System.Text.Json;
using GrimorioDev.Application.DTOs;
using GrimorioDev.Domain.Entities;
using GrimorioDev.Domain.Interfaces;
using GrimorioDev.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Repositories;

public sealed class CardRepository : Application.UseCases.ICardRepository
{
    private readonly WorkspaceSessionService _session;
    private readonly ILogger<CardRepository> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CardRepository(WorkspaceSessionService session, ILogger<CardRepository> logger)
    {
        _session = session;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Card>> GetAllAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var index = _session.Index;
        if (index is null)
        {
            _logger.LogWarning("Index not available for workspace {Id}", workspaceId);
            return Array.Empty<Card>();
        }

        var entries = index.EnumerateAllEntries().ToList();
        if (entries.Count == 0)
            return Array.Empty<Card>();

        var dataFile = _session.DataFile;
        var bloom = _session.Bloom;
        var cache = _session.Cache;
        var dedup = _session.DedupStore;

        var cards = new List<Card>(entries.Count);
        foreach (var (cardId, segIdx, offSeg) in entries)
        {
            try
            {
                if (cache is not null)
                {
                    var cached = cache.Get<byte[]>(cardId);
                    if (cached is not null)
                    {
                        var dto = JsonSerializer.Deserialize<CardDto>(cached, JsonOptions);
                        if (dto is not null)
                            cards.Add(RestoreCard(dto));
                        continue;
                    }
                }

                if (bloom is not null && !bloom.MightContain(cardId))
                    continue;

                if (dataFile is null) continue;

                byte[] bytes;
                try
                {
                    var data = await dataFile.ReadEntryAsync(segIdx, offSeg, ct).ConfigureAwait(false);
                    bytes = data.ToArray();
                }
                catch (DedupReferenceException dre)
                {
                    if (dedup is null) continue;
                    var blob = await dedup.LoadBlobAsync(dre.ContentHash, ct).ConfigureAwait(false);
                    bytes = blob.ToArray();
                }

                cache?.Set(cardId, bytes);
                var entryDto = JsonSerializer.Deserialize<CardDto>(bytes, JsonOptions);
                if (entryDto is not null)
                    cards.Add(RestoreCard(entryDto));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load card {Id} during GetAll", cardId);
            }
        }

        _logger.LogDebug("Loaded {Count} cards from LSM store", cards.Count);
        return cards;
    }

    public async Task<Card?> GetByIdAsync(Guid workspaceId, Guid cardId, CancellationToken ct = default)
    {
        var cache = _session.Cache;
        if (cache is not null)
        {
            var cached = cache.Get<byte[]>(cardId);
            if (cached is not null)
            {
                var dto = JsonSerializer.Deserialize<CardDto>(cached, JsonOptions);
                if (dto is not null) return RestoreCard(dto);
            }
        }

        var bloom = _session.Bloom;
        if (bloom is not null && !bloom.MightContain(cardId))
            return null;

        var index = _session.Index;
        if (index is null || !index.TryFind(cardId, out var segIdx, out var offSeg))
            return null;

        var dataFile = _session.DataFile;
        var dedup = _session.DedupStore;
        if (dataFile is null) return null;

        try
        {
            byte[] bytes;
            try
            {
                var data = await dataFile.ReadEntryAsync(segIdx, offSeg, ct).ConfigureAwait(false);
                bytes = data.ToArray();
            }
            catch (DedupReferenceException dre)
            {
                if (dedup is null) return null;
                var blob = await dedup.LoadBlobAsync(dre.ContentHash, ct).ConfigureAwait(false);
                bytes = blob.ToArray();
            }

            cache?.Set(cardId, bytes);
            var dto = JsonSerializer.Deserialize<CardDto>(bytes, JsonOptions);
            return dto is not null ? RestoreCard(dto) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load card {Id}", cardId);
            return null;
        }
    }

    public async Task SaveAsync(Guid workspaceId, Card card, CancellationToken ct = default)
    {
        var dto = CardDto.FromDomain(card);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);

        var dataFile = _session.DataFile;
        var index = _session.Index;
        var wal = _session.Wal;

        if (dataFile is null || index is null)
        {
            _logger.LogWarning("LSM storage not available for workspace {Id}", workspaceId);
            return;
        }

        try
        {
            if (wal is not null)
                await wal.AppendAsync(WalOperation.Update, card.Id, bytes, ct).ConfigureAwait(false);

            var (segIdx, offSeg) = await dataFile.AppendAsync(card.Id, bytes, compress: true, ct)
                .ConfigureAwait(false);
            index.Upsert(card.Id, segIdx, offSeg);
            _session.Bloom?.Add(card.Id);
            _session.Cache?.Invalidate(card.Id);

            _logger.LogDebug("Saved card {Id} at segment {Seg}, offset {Off}", card.Id, segIdx, offSeg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save card {Id}", card.Id);
            throw;
        }
    }

    public async Task DeleteAsync(Guid workspaceId, Guid cardId, CancellationToken ct = default)
    {
        var index = _session.Index;
        var wal = _session.Wal;

        if (index is null)
            return;

        try
        {
            if (wal is not null)
                await wal.AppendAsync(WalOperation.Delete, cardId, ReadOnlyMemory<byte>.Empty, ct)
                    .ConfigureAwait(false);

            index.Remove(cardId);
            _session.Cache?.Invalidate(cardId);
            _logger.LogDebug("Deleted card {Id}", cardId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete card {Id}", cardId);
            throw;
        }
    }

    public async Task SaveBatchAsync(Guid workspaceId, IReadOnlyList<Card> cards, CancellationToken ct = default)
    {
        foreach (var card in cards)
            await SaveAsync(workspaceId, card, ct).ConfigureAwait(false);
    }

    private static Card RestoreCard(CardDto dto) => Card.Restore(
        dto.Id, dto.Title, dto.Content,
        new CardPosition(dto.X, dto.Y, dto.ZIndex),
        dto.Width, dto.Height, dto.IsPinned,
        dto.CreatedAt, dto.UpdatedAt);
}
