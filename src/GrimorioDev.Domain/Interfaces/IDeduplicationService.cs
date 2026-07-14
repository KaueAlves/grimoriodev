namespace GrimorioDev.Domain.Interfaces;

public interface IDeduplicationService
{
    Task<(byte[] Hash, bool IsNew)> ComputeHashAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    Task<bool> BlobExistsAsync(byte[] hash, CancellationToken cancellationToken = default);
    Task StoreBlobAsync(byte[] hash, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    Task<ReadOnlyMemory<byte>> LoadBlobAsync(byte[] hash, CancellationToken cancellationToken = default);
}
