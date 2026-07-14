using GrimorioDev.Application.DTOs;
using GrimorioDev.Domain.Entities;
using GrimorioDev.Domain.Interfaces;
using GrimorioDev.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Application.Services;

public sealed class WorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(IWorkspaceRepository workspaceRepository, ILogger<WorkspaceService> logger)
    {
        _workspaceRepository = workspaceRepository;
        _logger = logger;
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating workspace '{Name}'", request.Name);

        var location = request.Location ?? _workspaceRepository.GetDefaultLocation();
        var workspace = Workspace.Create(request.Name, location, request.Description);

        Directory.CreateDirectory(workspace.Location);

        await _workspaceRepository.SaveAsync(workspace, cancellationToken);

        _logger.LogInformation("Workspace {Id} created at {Location}", workspace.Id, workspace.Location);

        return MapToDto(workspace);
    }

    public async Task<WorkspaceDto?> OpenWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Opening workspace {Id}", workspaceId);

        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);
        if (workspace is null)
        {
            _logger.LogWarning("Workspace {Id} not found", workspaceId);
            return null;
        }

        workspace.TouchOpened();
        await _workspaceRepository.SaveAsync(workspace, cancellationToken);

        return MapToDto(workspace);
    }

    public async Task<WorkspaceDto?> SaveWorkspaceAsync(SaveWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            _logger.LogWarning("Workspace {Id} not found for save", request.WorkspaceId);
            return null;
        }

        workspace.TouchSaved(request.CardCount, request.SizeBytes);
        await _workspaceRepository.SaveAsync(workspace, cancellationToken);

        return MapToDto(workspace);
    }

    public async Task<IReadOnlyList<WorkspaceDto>> ListWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        var workspaces = await _workspaceRepository.GetAllAsync(cancellationToken);
        return workspaces.Select(MapToDto).ToList();
    }

    public async Task<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting workspace {Id}", workspaceId);

        var exists = await _workspaceRepository.ExistsAsync(workspaceId, cancellationToken);
        if (!exists)
        {
            _logger.LogWarning("Workspace {Id} not found for deletion", workspaceId);
            return false;
        }

        await _workspaceRepository.DeleteAsync(workspaceId, cancellationToken);
        return true;
    }

    public async Task<WorkspaceDto?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);
        return workspace is not null ? MapToDto(workspace) : null;
    }

    private static WorkspaceDto MapToDto(Workspace workspace) => new(
        workspace.Id,
        workspace.Name,
        workspace.Description,
        workspace.Location,
        workspace.CreatedAt,
        workspace.UpdatedAt,
        workspace.LastOpenedAt,
        workspace.LastSavedAt,
        workspace.CardCount,
        workspace.SizeBytes);
}
