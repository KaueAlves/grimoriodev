namespace GrimorioDev.Domain.Entities;

public abstract class EntityBase
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;

    protected void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
