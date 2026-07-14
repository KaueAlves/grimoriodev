namespace GrimorioDev.Domain.Interfaces;

public enum WalOperation : byte
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Batch = 3
}

public interface IWalService
{
    Task AppendAsync(WalOperation operation, Guid cardId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalEntry>> ReplayAsync(CancellationToken cancellationToken = default);
    Task TruncateAsync(CancellationToken cancellationToken = default);
    Task CompactAsync(CancellationToken cancellationToken = default);
    int PendingEntries { get; }
}

public readonly record struct WalEntry
{
    public WalOperation Operation { get; init; }
    public Guid CardId { get; init; }
    public DateTime Timestamp { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }
}
