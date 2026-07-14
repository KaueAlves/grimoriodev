using System.Text.Json.Serialization;
using GrimorioDev.Domain.ValueObjects;
using GrimorioDev.Infrastructure.Repositories;

namespace GrimorioDev.Infrastructure.Serialization;

[JsonSerializable(typeof(WorkspaceMeta))]
[JsonSerializable(typeof(WorkspaceSettings))]
[JsonSerializable(typeof(RecentWorkspacesFile))]
[JsonSerializable(typeof(RecentWorkspaceEntry))]
internal partial class WorkspaceJsonContext : JsonSerializerContext;

public sealed class RecentWorkspacesFile
{
    public List<RecentWorkspaceEntry> Entries { get; set; } = new();
}

public sealed class RecentWorkspaceEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime LastOpenedAt { get; set; }
}
