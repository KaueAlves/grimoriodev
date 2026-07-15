using System.Windows;
using GrimorioDev.Presentation.ViewModels;

namespace GrimorioDev.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly CanvasPage _canvasPage;

    public MainWindow(MainViewModel viewModel, CanvasPage canvasPage)
    {
        InitializeComponent();
        DataContext = viewModel;
        _canvasPage = canvasPage;
    }

    public void NavigateToCanvas()
    {
        ContentFrame.Navigate(_canvasPage);
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }
}
