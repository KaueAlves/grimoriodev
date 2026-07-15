namespace GrimorioDev.Application.DTOs;

public sealed record CreateCardRequest(
    string Title,
    string Content,
    double X,
    double Y,
    int ZIndex,
    double Width = 300,
    double Height = 200);
