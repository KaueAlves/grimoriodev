using System.Windows;
using GrimorioDev.Presentation.ViewModels;

namespace GrimorioDev.Presentation.Views;

public partial class StartScreenWindow : Window
{
    private readonly WorkspaceViewModel _viewModel;

    public StartScreenWindow(WorkspaceViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadRecentWorkspacesCommand.ExecuteAsync(null);
    }
}
