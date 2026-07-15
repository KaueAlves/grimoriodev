using GrimorioDev.Application.Services;
using GrimorioDev.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace GrimorioDev.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<DirtyTrackerService>();
        services.AddSingleton<AutoSaveService>();
        services.AddTransient<WorkspaceService>();
        services.AddTransient<LoadCanvasCards>();
        services.AddTransient<MoveCard>();
        services.AddTransient<CreateCard>();
        return services;
    }
}
