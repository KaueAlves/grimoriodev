using System.Windows;
using GrimorioDev.Application;
using GrimorioDev.Infrastructure;
using GrimorioDev.Presentation.ViewModels;
using GrimorioDev.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GrimorioDev.Presentation;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        services.AddApplication();
        services.AddInfrastructure();
        services.AddPresentation();

        ServiceProvider = services.BuildServiceProvider();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
        return services;
    }
}
