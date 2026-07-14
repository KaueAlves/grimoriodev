namespace GrimorioDev.Application.DTOs;

public sealed record CreateWorkspaceRequest(
    string Name,
    string Description = "",
    string? Location = null);

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string Description,
    string Location,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime LastOpenedAt,
    DateTime LastSavedAt,
    int CardCount,
    long SizeBytes);

public sealed record SaveWorkspaceRequest(
    Guid WorkspaceId,
    int CardCount,
    long SizeBytes);
