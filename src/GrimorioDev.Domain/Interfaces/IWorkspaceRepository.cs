using GrimorioDev.Domain.Entities;

namespace GrimorioDev.Domain.Interfaces;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(Workspace workspace, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    string GetDefaultLocation();
}
