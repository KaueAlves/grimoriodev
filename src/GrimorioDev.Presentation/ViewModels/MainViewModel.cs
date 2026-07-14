using CommunityToolkit.Mvvm.ComponentModel;
using GrimorioDev.Application.DTOs;
using GrimorioDev.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Presentation.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly WorkspaceSessionService _session;

    [ObservableProperty]
    private string _title = "GrimórioDev";

    [ObservableProperty]
    private string _workspaceName = string.Empty;

    [ObservableProperty]
    private string _workspaceDescription = string.Empty;

    [ObservableProperty]
    private int _cardCount;

    [ObservableProperty]
    private string _sizeInfo = string.Empty;

    [ObservableProperty]
    private string _statusText = "🟢 Salvo";

    [ObservableProperty]
    private bool _hasWorkspace;

    public MainViewModel(ILogger<MainViewModel> logger, WorkspaceSessionService session)
    {
        _logger = logger;
        _session = session;
    }

    public void LoadWorkspace(WorkspaceDto workspace)
    {
        WorkspaceName = workspace.Name;
        WorkspaceDescription = workspace.Description;
        CardCount = workspace.CardCount;
        Title = $"GrimórioDev — {workspace.Name}";
        SizeInfo = FormatSize(workspace.SizeBytes);
        HasWorkspace = true;
        StatusText = "🟢 Workspace aberto";

        _logger.LogInformation("Workspace '{Name}' loaded in MainViewModel", workspace.Name);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}
