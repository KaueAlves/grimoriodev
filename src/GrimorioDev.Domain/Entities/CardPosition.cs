namespace GrimorioDev.Domain.Entities;

public readonly record struct CardPosition(double X, double Y, int ZIndex)
{
    public CardPosition Offset(double dx, double dy) => new(X + dx, Y + dy, ZIndex);
}
