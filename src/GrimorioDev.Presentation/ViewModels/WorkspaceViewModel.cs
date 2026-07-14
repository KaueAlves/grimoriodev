using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrimorioDev.Application.DTOs;
using GrimorioDev.Application.Services;
using GrimorioDev.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GrimorioDev.Presentation.ViewModels;

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly WorkspaceService _workspaceService;
    private readonly WorkspaceSessionService _sessionService;
    private readonly AutoSaveService _autoSaveService;
    private readonly ILogger<WorkspaceViewModel> _logger;

    [ObservableProperty]
    private string _newWorkspaceName = string.Empty;

    [ObservableProperty]
    private string _newWorkspaceDescription = string.Empty;

    [ObservableProperty]
    private string _newWorkspaceLocation = string.Empty;

    [ObservableProperty]
    private bool _isCreatingWorkspace;

    [ObservableProperty]
    private WorkspaceDto? _selectedWorkspace;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<WorkspaceDto> RecentWorkspaces { get; } = new();

    public event Action<WorkspaceDto>? WorkspaceOpened;

    public WorkspaceViewModel(
        WorkspaceService workspaceService,
        WorkspaceSessionService sessionService,
        AutoSaveService autoSaveService,
        ILogger<WorkspaceViewModel> logger)
    {
        _workspaceService = workspaceService;
        _sessionService = sessionService;
        _autoSaveService = autoSaveService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadRecentWorkspacesAsync()
    {
        IsBusy = true;
        StatusMessage = "Carregando...";
        try
        {
            var workspaces = await _workspaceService.ListWorkspacesAsync();
            RecentWorkspaces.Clear();
            foreach (var ws in workspaces)
                RecentWorkspaces.Add(ws);
            StatusMessage = RecentWorkspaces.Count > 0
                ? $"{RecentWorkspaces.Count} workspace(s) encontrado(s)"
                : "Nenhum workspace encontrado";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recent workspaces");
            StatusMessage = "Erro ao carregar workspaces";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ShowCreateWorkspace()
    {
        NewWorkspaceName = string.Empty;
        NewWorkspaceDescription = string.Empty;
        NewWorkspaceLocation = string.Empty;
        IsCreatingWorkspace = true;
    }

    [RelayCommand]
    private void CancelCreateWorkspace()
    {
        IsCreatingWorkspace = false;
    }

    [RelayCommand]
    private async Task CreateWorkspaceAsync()
    {
        if (string.IsNullOrWhiteSpace(NewWorkspaceName))
            return;

        IsBusy = true;
        StatusMessage = "Criando workspace...";
        try
        {
            var request = new CreateWorkspaceRequest(
                NewWorkspaceName.Trim(),
                NewWorkspaceDescription.Trim(),
                string.IsNullOrWhiteSpace(NewWorkspaceLocation) ? null : NewWorkspaceLocation.Trim());

            var workspace = await _workspaceService.CreateWorkspaceAsync(request);

            RecentWorkspaces.Insert(0, workspace);
            IsCreatingWorkspace = false;

            await InitializeSessionAndOpenAsync(workspace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workspace");
            StatusMessage = "Erro ao criar workspace";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenWorkspaceAsync(WorkspaceDto? workspace)
    {
        if (workspace is null) return;

        IsBusy = true;
        StatusMessage = $"Abrindo '{workspace.Name}'...";
        try
        {
            var opened = await _workspaceService.OpenWorkspaceAsync(workspace.Id);
            if (opened is not null)
            {
                await InitializeSessionAndOpenAsync(opened);
            }
            else
            {
                StatusMessage = "Workspace não encontrado";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open workspace");
            StatusMessage = "Erro ao abrir workspace";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InitializeSessionAndOpenAsync(WorkspaceDto workspace)
    {
        StatusMessage = "Inicializando motor de dados...";

        await _sessionService.InitializeAsync(workspace);

        if (_sessionService.Wal is not null)
        {
            var replayCount = await ReplayWalAsync();
            if (replayCount > 0)
                _logger.LogInformation("WAL replay: {Count} entries restored", replayCount);
        }

        _autoSaveService.Start(workspace.Id);

        StatusMessage = $"Workspace '{workspace.Name}' pronto";
        WorkspaceOpened?.Invoke(workspace);
    }

    private async Task<int> ReplayWalAsync()
    {
        var wal = _sessionService.Wal;
        var index = _sessionService.Index;
        var bloom = _sessionService.Bloom;
        var dataFile = _sessionService.DataFile;

        if (wal is null || index is null || bloom is null || dataFile is null)
            return 0;

        var entries = await wal.ReplayAsync();
        var applied = 0;

        foreach (var entry in entries)
        {
            try
            {
                switch (entry.Operation)
                {
                    case Domain.Interfaces.WalOperation.Create:
                    case Domain.Interfaces.WalOperation.Update:
                        var result = await dataFile.AppendAsync(
                            entry.CardId, entry.Payload, compress: false);
                        index.Upsert(entry.CardId, result.SegmentIndex, result.Offset);
                        bloom.Add(entry.CardId);
                        applied++;
                        break;

                    case Domain.Interfaces.WalOperation.Delete:
                        index.Remove(entry.CardId);
                        applied++;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WAL replay: failed for card {CardId}", entry.CardId);
            }
        }

        await wal.TruncateAsync();
        return applied;
    }

    [RelayCommand]
    private async Task DeleteWorkspaceAsync(WorkspaceDto? workspace)
    {
        if (workspace is null) return;

        var result = MessageBox.Show(
            $"Excluir workspace '{workspace.Name}'?\nEsta ação não pode ser desfeita.",
            "Confirmar Exclusão",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _workspaceService.DeleteWorkspaceAsync(workspace.Id);
            RecentWorkspaces.Remove(workspace);
            StatusMessage = "Workspace excluído";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete workspace");
            StatusMessage = "Erro ao excluir workspace";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BrowseWorkspaceAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar arquivo de workspace",
            Filter = "Workspace files (*.json)|*.json",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        var path = dialog.FileName;
        var id = Guid.Parse(Path.GetFileNameWithoutExtension(path));
        var workspace = await _workspaceService.GetWorkspaceAsync(id);
        if (workspace is not null)
            await OpenWorkspaceAsync(workspace);
    }
}
