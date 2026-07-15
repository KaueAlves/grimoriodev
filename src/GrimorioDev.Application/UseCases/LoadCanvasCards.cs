using GrimorioDev.Application.DTOs;
using GrimorioDev.Application.Interfaces;
using GrimorioDev.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Application.UseCases;

public sealed class LoadCanvasCards
{
    private readonly ICardRepository _cardRepo;
    private readonly IWorkspaceSessionService _session;
    private readonly ILogger<LoadCanvasCards> _logger;

    public LoadCanvasCards(
        ICardRepository cardRepo,
        IWorkspaceSessionService session,
        ILogger<LoadCanvasCards> logger)
    {
        _cardRepo = cardRepo;
        _session = session;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CardDto>> ExecuteAsync(CancellationToken ct = default)
    {
        if (_session.CurrentWorkspace is null)
        {
            _logger.LogWarning("No active workspace to load cards from");
            return Array.Empty<CardDto>();
        }

        var wsId = _session.CurrentWorkspace.Id;
        var cards = await _cardRepo.GetAllAsync(wsId, ct);
        _logger.LogDebug("Loaded {Count} cards from workspace {Id}", cards.Count, wsId);
        return cards.Select(CardDto.FromDomain).ToList();
    }
}
