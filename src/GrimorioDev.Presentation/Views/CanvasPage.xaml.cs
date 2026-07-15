using System.Windows.Controls;
using GrimorioDev.Presentation.Controls;
using GrimorioDev.Presentation.ViewModels;

namespace GrimorioDev.Presentation.Views;

public partial class CanvasPage : Page
{
    public CanvasViewModel ViewModel => (CanvasViewModel)DataContext;

    public CanvasPage(CanvasViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) =>
        {
            CanvasControl.CardSelected += OnCardSelected;
            CanvasControl.CardMoved += OnCardMoved;
            CanvasControl.CardDeselected += OnCardDeselected;
            CanvasControl.CanvasDoubleClicked += OnCanvasDoubleClicked;
            await viewModel.LoadCardsAsync();
        };

        Unloaded += (_, _) =>
        {
            CanvasControl.CardSelected -= OnCardSelected;
            CanvasControl.CardMoved -= OnCardMoved;
            CanvasControl.CardDeselected -= OnCardDeselected;
            CanvasControl.CanvasDoubleClicked -= OnCanvasDoubleClicked;
        };
    }

    private void OnCardSelected(object? sender, CardRenderData card)
    {
        ViewModel.HandleCardSelected(card);
    }

    private void OnCardDeselected()
    {
        ViewModel.HandleCardDeselected();
    }

    private async void OnCardMoved(object? sender, CardMovedEventArgs e)
    {
        await ViewModel.HandleCardMoved(e.CardId, e.NewX, e.NewY);
    }

    private async void OnCanvasDoubleClicked(double canvasX, double canvasY)
    {
        await ViewModel.HandleCanvasDoubleClick(canvasX, canvasY);
    }
}
