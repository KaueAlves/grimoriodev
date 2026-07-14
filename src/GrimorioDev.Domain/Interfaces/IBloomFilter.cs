namespace GrimorioDev.Domain.Interfaces;

public interface IBloomFilter
{
    void Add(Guid cardId);
    bool MightContain(Guid cardId);
    void Remove(Guid cardId);
    void Clear();
    int Count { get; }
}
