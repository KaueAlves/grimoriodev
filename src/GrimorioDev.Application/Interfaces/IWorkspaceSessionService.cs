using GrimorioDev.Application.DTOs;

namespace GrimorioDev.Application.Interfaces;

public interface IWorkspaceSessionService
{
    WorkspaceDto? CurrentWorkspace { get; }
    bool IsActive { get; }
}
