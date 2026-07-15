using GrimorioDev.Application.DTOs;
using GrimorioDev.Application.Interfaces;
using GrimorioDev.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Application.UseCases;

public sealed class MoveCard
{
    private readonly ICardRepository _cardRepo;
    private readonly IWorkspaceSessionService _session;
    private readonly ILogger<MoveCard> _logger;

    public MoveCard(
        ICardRepository cardRepo,
        IWorkspaceSessionService session,
        ILogger<MoveCard> logger)
    {
        _cardRepo = cardRepo;
        _session = session;
        _logger = logger;
    }

    public async Task<CardDto?> ExecuteAsync(MoveCardRequest request, CancellationToken ct = default)
    {
        if (_session.CurrentWorkspace is null)
        {
            _logger.LogWarning("No active workspace to move card in");
            return null;
        }

        var wsId = _session.CurrentWorkspace.Id;
        var card = await _cardRepo.GetByIdAsync(wsId, request.CardId, ct);
        if (card is null)
        {
            _logger.LogWarning("Card {Id} not found", request.CardId);
            return null;
        }

        card.MoveTo(new CardPosition(request.NewX, request.NewY, request.NewZIndex));
        await _cardRepo.SaveAsync(wsId, card, ct);
        _logger.LogDebug("Moved card {Id} to ({X}, {Y})", request.CardId, request.NewX, request.NewY);
        return CardDto.FromDomain(card);
    }
}
