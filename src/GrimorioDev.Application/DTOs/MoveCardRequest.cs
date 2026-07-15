namespace GrimorioDev.Application.DTOs;

public sealed record MoveCardRequest(Guid CardId, double NewX, double NewY, int NewZIndex);
