using GrimorioDev.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GrimorioDev.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<DirtyTrackerService>();
        services.AddSingleton<AutoSaveService>();
        services.AddTransient<WorkspaceService>();
        return services;
    }
}
