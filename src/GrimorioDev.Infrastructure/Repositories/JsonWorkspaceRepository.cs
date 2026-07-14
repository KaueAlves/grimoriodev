using System.Text.Json;
using System.Text.Json.Serialization;
using GrimorioDev.Domain.Entities;
using GrimorioDev.Domain.Interfaces;
using GrimorioDev.Domain.ValueObjects;
using GrimorioDev.Infrastructure.Serialization;
using Microsoft.Extensions.Logging;

namespace GrimorioDev.Infrastructure.Repositories;

public sealed class JsonWorkspaceRepository : IWorkspaceRepository
{
    private readonly string _appDataPath;
    private readonly ILogger<JsonWorkspaceRepository> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonWorkspaceRepository(ILogger<JsonWorkspaceRepository> logger)
    {
        _logger = logger;
        _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GrimorioDev");
        Directory.CreateDirectory(_appDataPath);
    }

    public string GetDefaultLocation() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "GrimorioDev");

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filePath = GetWorkspaceFilePath(id);
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var meta = JsonSerializer.Deserialize(json, WorkspaceJsonContext.Default.WorkspaceMeta);
            if (meta is null) return null;

            return Workspace.Restore(
                id, meta.Name, GetWorkspaceDataDir(id), meta.Description,
                meta.Settings, meta.CreatedAt, meta.UpdatedAt,
                meta.LastOpenedAt, meta.LastSavedAt, meta.CardCount, meta.SizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load workspace {Id}", id);
            return null;
        }
    }

    public async Task<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var workspaces = new List<Workspace>();
        var dir = GetWorkspacesDir();

        if (!Directory.Exists(dir))
            return workspaces;

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var id = Guid.Parse(Path.GetFileNameWithoutExtension(file));
                var ws = await GetByIdAsync(id, cancellationToken);
                if (ws is not null)
                    workspaces.Add(ws);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load workspace from {File}", file);
            }
        }

        return workspaces.OrderByDescending(w => w.LastOpenedAt).ToList();
    }

    public async Task SaveAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        var dir = GetWorkspacesDir();
        Directory.CreateDirectory(dir);

        var meta = new WorkspaceMeta
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Description = workspace.Description,
            Settings = workspace.Settings,
            CreatedAt = workspace.CreatedAt,
            UpdatedAt = workspace.UpdatedAt,
            LastOpenedAt = workspace.LastOpenedAt,
            LastSavedAt = workspace.LastSavedAt,
            CardCount = workspace.CardCount,
            SizeBytes = workspace.SizeBytes
        };

        var json = JsonSerializer.Serialize(meta, WorkspaceJsonContext.Default.WorkspaceMeta);
        var filePath = Path.Combine(dir, $"{workspace.Id}.json");
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        await UpdateRecentListAsync(workspace, cancellationToken);
        _logger.LogDebug("Workspace {Id} saved to {Path}", workspace.Id, filePath);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filePath = GetWorkspaceFilePath(id);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogDebug("Workspace {Id} metadata deleted", id);
        }

        var dataDir = GetWorkspaceDataDir(id);
        if (Directory.Exists(dataDir))
            Directory.Delete(dataDir, recursive: true);

        await RemoveFromRecentListAsync(id, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(GetWorkspaceFilePath(id)));
    }

    public async Task<IReadOnlyList<RecentWorkspaceEntry>> GetRecentAsync(int maxCount = 10, CancellationToken cancellationToken = default)
    {
        var recentPath = Path.Combine(_appDataPath, "recent.json");
        var recent = await LoadRecentAsync(recentPath, cancellationToken);
        return recent.Entries.Take(maxCount).ToList();
    }

    private string GetWorkspacesDir() => Path.Combine(_appDataPath, "workspaces");
    private string GetWorkspaceFilePath(Guid id) => Path.Combine(GetWorkspacesDir(), $"{id}.json");
    private string GetWorkspaceDataDir(Guid id) => Path.Combine(_appDataPath, "data", id.ToString());

    private async Task UpdateRecentListAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        var recentPath = Path.Combine(_appDataPath, "recent.json");
        var recent = await LoadRecentAsync(recentPath, cancellationToken);

        recent.Entries.RemoveAll(e => e.Id == workspace.Id);
        recent.Entries.Insert(0, new RecentWorkspaceEntry
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Location = workspace.Location,
            LastOpenedAt = DateTime.UtcNow
        });

        if (recent.Entries.Count > 20)
            recent.Entries = recent.Entries.Take(20).ToList();

        var json = JsonSerializer.Serialize(recent, JsonOptions);
        await File.WriteAllTextAsync(recentPath, json, cancellationToken);
    }

    private async Task RemoveFromRecentListAsync(Guid id, CancellationToken cancellationToken)
    {
        var recentPath = Path.Combine(_appDataPath, "recent.json");
        var recent = await LoadRecentAsync(recentPath, cancellationToken);
        recent.Entries.RemoveAll(e => e.Id == id);

        var json = JsonSerializer.Serialize(recent, JsonOptions);
        await File.WriteAllTextAsync(recentPath, json, cancellationToken);
    }

    private async Task<RecentWorkspacesFile> LoadRecentAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return new RecentWorkspacesFile();

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<RecentWorkspacesFile>(json, JsonOptions) ?? new RecentWorkspacesFile();
        }
        catch
        {
            return new RecentWorkspacesFile();
        }
    }
}

internal sealed class WorkspaceMeta
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkspaceSettings Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastOpenedAt { get; set; }
    public DateTime LastSavedAt { get; set; }
    public int CardCount { get; set; }
    public long SizeBytes { get; set; }
}
