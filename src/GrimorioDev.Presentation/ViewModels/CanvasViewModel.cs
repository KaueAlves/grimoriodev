using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrimorioDev.Application.DTOs;
using GrimorioDev.Application.UseCases;
using GrimorioDev.Presentation.Controls;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Presentation.ViewModels;

public partial class CanvasViewModel : ObservableObject
{
    private readonly LoadCanvasCards _loadCards;
    private readonly MoveCard _moveCard;
    private readonly CreateCard _createCard;
    private readonly ILogger<CanvasViewModel> _logger;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private string _statusText = "🟢 Canvas pronto";

    [ObservableProperty]
    private CardRenderData? _selectedCard;

    public ObservableCollection<CardRenderData> Cards { get; } = new();

    public event Action<CardRenderData>? CardSelectedChanged;

    public CanvasViewModel(
        LoadCanvasCards loadCards,
        MoveCard moveCard,
        CreateCard createCard,
        ILogger<CanvasViewModel> logger)
    {
        _loadCards = loadCards;
        _moveCard = moveCard;
        _createCard = createCard;
        _logger = logger;
    }

    [RelayCommand]
    public async Task LoadCardsAsync()
    {
        try
        {
            StatusText = "🔄 Carregando cards...";
            var cards = await _loadCards.ExecuteAsync();
            Cards.Clear();
            foreach (var card in cards)
                Cards.Add(ToRenderData(card));
            StatusText = $"🟢 {cards.Count} cards carregados";
            _logger.LogInformation("Loaded {Count} cards into canvas", cards.Count);
        }
        catch (Exception ex)
        {
            StatusText = "🔴 Erro ao carregar cards";
            _logger.LogError(ex, "Failed to load canvas cards");
        }
    }

    public async Task HandleCardMoved(Guid cardId, double newX, double newY)
    {
        var existing = Cards.FirstOrDefault(c => c.Id == cardId);
        if (existing is null) return;

        var idx = Cards.IndexOf(existing);
        Cards[idx] = existing with { X = newX, Y = newY };
        SelectedCard = Cards[idx];

        try
        {
            var request = new MoveCardRequest(cardId, newX, newY, existing.ZIndex);
            var result = await _moveCard.ExecuteAsync(request);
            if (result is not null)
                Cards[idx] = ToRenderData(result);
            StatusText = $"🟢 Card movido para ({newX:F0}, {newY:F0})";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist move for card {Id}", cardId);
            StatusText = "🔴 Erro ao salvar posição";
        }
    }

    public void HandleCardSelected(CardRenderData card)
    {
        SelectedCard = card;
        StatusText = $"🔵 Card: {card.Title} ({card.X:F0}, {card.Y:F0})";
        CardSelectedChanged?.Invoke(card);
    }

    public void HandleCardDeselected()
    {
        SelectedCard = null;
        StatusText = "🟢 Canvas pronto";
        CardSelectedChanged?.Invoke(null!);
    }

    public async Task HandleCanvasDoubleClick(double canvasX, double canvasY)
    {
        var request = new CreateCardRequest(
            "Novo Card",
            string.Empty,
            canvasX,
            canvasY,
            0);

        try
        {
            var result = await _createCard.ExecuteAsync(request);
            if (result is not null)
            {
                Cards.Add(ToRenderData(result));
                StatusText = $"🟢 Card criado em ({canvasX:F0}, {canvasY:F0})";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create card");
            StatusText = "🔴 Erro ao criar card";
        }
    }

    public void SetZoom(double zoom)
    {
        ZoomLevel = zoom;
        StatusText = $"🔍 Zoom: {zoom * 100:F0}%";
    }

    public void UpdateCanvasPosition(Guid cardId, double x, double y)
    {
        var existing = Cards.FirstOrDefault(c => c.Id == cardId);
        if (existing is null) return;
        var idx = Cards.IndexOf(existing);
        Cards[idx] = existing with { X = x, Y = y };
    }

    private static CardRenderData ToRenderData(CardDto dto) => new(
        dto.Id, dto.X, dto.Y, dto.ZIndex,
        dto.Width, dto.Height, dto.Title, dto.Content, dto.IsPinned);
}
