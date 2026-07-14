using GrimorioDev.Domain.Interfaces;
using GrimorioDev.Infrastructure.Repositories;
using GrimorioDev.Infrastructure.Services;
using GrimorioDev.Infrastructure.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GrimorioDev.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/grimoriodev-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        services.AddSingleton<IWorkspaceRepository, JsonWorkspaceRepository>();
        services.AddSingleton<Lz4CompressionService>();
        services.AddSingleton<MemoryBudgetManager>();

        return services;
    }

    public static IServiceCollection AddWorkspaceServices(this IServiceCollection services, string workspacePath)
    {
        services.AddSingleton(_ => new DataFileRepository(
            workspacePath,
            _.GetRequiredService<Lz4CompressionService>(),
            _.GetRequiredService<ILogger<DataFileRepository>>()));

        services.AddSingleton(_ => new MemoryMappedIndexRepository(
            workspacePath,
            _.GetRequiredService<ILogger<MemoryMappedIndexRepository>>()));

        services.AddSingleton(_ => new MemoryMappedIndexBloomFilter(
            workspacePath,
            _.GetRequiredService<ILogger<MemoryMappedIndexBloomFilter>>()));

        services.AddSingleton(_ => new WalService(
            workspacePath,
            _.GetRequiredService<ILogger<WalService>>()));

        services.AddSingleton(_ => new ContentAddressableStore(
            workspacePath,
            _.GetRequiredService<ILogger<ContentAddressableStore>>()));

        services.AddSingleton(_ => new CardCacheLru2Q(
            _.GetRequiredService<ILogger<CardCacheLru2Q>>(),
            _.GetRequiredService<MemoryBudgetManager>()));

        services.AddSingleton(_ => new Prefetcher(
            _.GetRequiredService<DataFileRepository>(),
            _.GetRequiredService<MemoryMappedIndexRepository>(),
            _.GetRequiredService<ILogger<Prefetcher>>()));

        services.AddTransient<LoadCard>();
        services.AddTransient<RecoverWorkspace>();
        services.AddTransient<CompactWal>();
        services.AddTransient<VacuumData>();

        return services;
    }
}
