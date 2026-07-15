using System.Windows;
using GrimorioDev.Application;
using GrimorioDev.Application.DTOs;
using GrimorioDev.Infrastructure;
using GrimorioDev.Presentation.ViewModels;
using GrimorioDev.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GrimorioDev.Presentation;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        services.AddApplication();
        services.AddInfrastructure();
        services.AddPresentation();

        ServiceProvider = services.BuildServiceProvider();

        var startScreen = ServiceProvider.GetRequiredService<StartScreenWindow>();
        var workspaceViewModel = ServiceProvider.GetRequiredService<WorkspaceViewModel>();
        workspaceViewModel.WorkspaceOpened += OnWorkspaceOpened;

        Current.MainWindow = startScreen;
        startScreen.Show();
    }

    private static void OnWorkspaceOpened(WorkspaceDto workspace)
    {
        Current.Dispatcher.Invoke(() =>
        {
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            var mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
            mainViewModel.LoadWorkspace(workspace);

            Current.MainWindow?.Close();
            Current.MainWindow = mainWindow;
            mainWindow.Show();
            mainWindow.NavigateToCanvas();
        });
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
        services.AddSingleton<WorkspaceViewModel>();
        services.AddTransient<StartScreenWindow>();
        services.AddTransient<CanvasViewModel>();
        services.AddTransient<CanvasPage>();
        return services;
    }
}
