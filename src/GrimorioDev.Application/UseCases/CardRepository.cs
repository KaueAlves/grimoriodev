using GrimorioDev.Domain.Entities;

namespace GrimorioDev.Application.UseCases;

public interface ICardRepository
{
    Task<IReadOnlyList<Card>> GetAllAsync(Guid workspaceId, CancellationToken ct = default);
    Task<Card?> GetByIdAsync(Guid workspaceId, Guid cardId, CancellationToken ct = default);
    Task SaveAsync(Guid workspaceId, Card card, CancellationToken ct = default);
    Task DeleteAsync(Guid workspaceId, Guid cardId, CancellationToken ct = default);
    Task SaveBatchAsync(Guid workspaceId, IReadOnlyList<Card> cards, CancellationToken ct = default);
}
