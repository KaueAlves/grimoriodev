using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrimorioDev.Application.DTOs;
using GrimorioDev.Application.Services;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Presentation.ViewModels;

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly WorkspaceService _workspaceService;
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

    public ObservableCollection<WorkspaceDto> RecentWorkspaces { get; } = new();

    public WorkspaceViewModel(WorkspaceService workspaceService, ILogger<WorkspaceViewModel> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadRecentWorkspacesAsync()
    {
        IsBusy = true;
        try
        {
            var workspaces = await _workspaceService.ListWorkspacesAsync();
            RecentWorkspaces.Clear();
            foreach (var ws in workspaces)
                RecentWorkspaces.Add(ws);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recent workspaces");
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
        try
        {
            var request = new CreateWorkspaceRequest(
                NewWorkspaceName.Trim(),
                NewWorkspaceDescription.Trim(),
                string.IsNullOrWhiteSpace(NewWorkspaceLocation) ? null : NewWorkspaceLocation.Trim());

            var workspace = await _workspaceService.CreateWorkspaceAsync(request);

            RecentWorkspaces.Insert(0, workspace);
            IsCreatingWorkspace = false;

            _logger.LogInformation("Workspace '{Name}' created successfully", workspace.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workspace");
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
        try
        {
            var opened = await _workspaceService.OpenWorkspaceAsync(workspace.Id);
            if (opened is not null)
            {
                _logger.LogInformation("Workspace '{Name}' opened", opened.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open workspace");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteWorkspaceAsync(WorkspaceDto? workspace)
    {
        if (workspace is null) return;

        var result = MessageBox.Show(
            $"Delete workspace '{workspace.Name}'?\nThis cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _workspaceService.DeleteWorkspaceAsync(workspace.Id);
            RecentWorkspaces.Remove(workspace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete workspace");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
