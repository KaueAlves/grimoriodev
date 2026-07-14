namespace GrimorioDev.Domain.Entities;

[Flags]
public enum CardFlags : byte
{
    None = 0,
    Compressed = 1 << 0,
    Deleted = 1 << 1,
    Pinned = 1 << 2,
    Deduplicated = 1 << 3
}

public readonly record struct CardIndexEntry
{
    public const int SizeInBytes = 32;

    public Guid CardId { get; init; }
    public uint TypeHash { get; init; }
    public long UpdatedAtTicks { get; init; }
    public ushort SizeKB { get; init; }
    public CardFlags Flags { get; init; }
    public byte RelevanceScore { get; init; }

    public bool IsDeleted => (Flags & CardFlags.Deleted) != 0;
    public bool IsCompressed => (Flags & CardFlags.Compressed) != 0;
    public bool IsPinned => (Flags & CardFlags.Pinned) != 0;
    public bool IsDeduplicated => (Flags & CardFlags.Deduplicated) != 0;

    public static uint ComputeTypeHash(ReadOnlySpan<byte> typeData)
    {
        const uint FnvOffsetBasis = 2166136261;
        const uint FnvPrime = 16777619;

        uint hash = FnvOffsetBasis;
        foreach (byte b in typeData)
        {
            hash ^= b;
            hash *= FnvPrime;
        }
        return hash;
    }
}
