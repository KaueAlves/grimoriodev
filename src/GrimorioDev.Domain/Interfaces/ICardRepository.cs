namespace GrimorioDev.Domain.Interfaces;

public interface ICardRepository
{
    Task<T?> LoadCardAsync<T>(Guid cardId, CancellationToken cancellationToken = default) where T : class;
    Task SaveCardAsync<T>(Guid cardId, T card, CancellationToken cancellationToken = default) where T : class;
    Task DeleteCardAsync(Guid cardId, CancellationToken cancellationToken = default);
    Task<bool> CardExistsAsync(Guid cardId, CancellationToken cancellationToken = default);
}
