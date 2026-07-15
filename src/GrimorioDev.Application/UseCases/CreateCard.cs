using GrimorioDev.Application.DTOs;
using GrimorioDev.Application.Interfaces;
using GrimorioDev.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Application.UseCases;

public sealed class CreateCard
{
    private readonly ICardRepository _cardRepo;
    private readonly IWorkspaceSessionService _session;
    private readonly ILogger<CreateCard> _logger;

    public CreateCard(
        ICardRepository cardRepo,
        IWorkspaceSessionService session,
        ILogger<CreateCard> logger)
    {
        _cardRepo = cardRepo;
        _session = session;
        _logger = logger;
    }

    public async Task<CardDto?> ExecuteAsync(CreateCardRequest request, CancellationToken ct = default)
    {
        if (_session.CurrentWorkspace is null)
        {
            _logger.LogWarning("No active workspace to create card in");
            return null;
        }

        var wsId = _session.CurrentWorkspace.Id;
        var card = Card.Create(
            request.Title,
            new CardPosition(request.X, request.Y, request.ZIndex),
            request.Width,
            request.Height);

        if (!string.IsNullOrEmpty(request.Content))
            card.UpdateContent(request.Content);

        await _cardRepo.SaveAsync(wsId, card, ct);
        _logger.LogInformation("Created card {Id} at ({X}, {Y})", card.Id, request.X, request.Y);
        return CardDto.FromDomain(card);
    }
}
