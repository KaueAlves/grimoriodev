using System.Windows;
using GrimorioDev.Presentation.ViewModels;

namespace GrimorioDev.Presentation.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
