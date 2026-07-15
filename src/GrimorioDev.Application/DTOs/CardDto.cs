using GrimorioDev.Domain.Entities;

namespace GrimorioDev.Application.DTOs;

public sealed record CardDto(
    Guid Id,
    string Title,
    string Content,
    double X,
    double Y,
    int ZIndex,
    double Width,
    double Height,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CardDto FromDomain(Card card) => new(
        card.Id,
        card.Title,
        card.Content,
        card.Position.X,
        card.Position.Y,
        card.Position.ZIndex,
        card.Width,
        card.Height,
        card.IsPinned,
        card.CreatedAt,
        card.UpdatedAt);
}
