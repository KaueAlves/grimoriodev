namespace GrimorioDev.Domain.Interfaces;

public interface ICardCache
{
    T? Get<T>(Guid cardId) where T : class;
    void Set<T>(Guid cardId, T card) where T : class;
    void Invalidate(Guid cardId);
    void Clear();
    int Count { get; }
}
