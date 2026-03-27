using System.Text.Json;
using IntuneMonitor.Config;
using IntuneMonitor.Models;

namespace IntuneMonitor.Storage;

/// <summary>
/// Stores and loads Intune backups as JSON files on the local file system.
/// </summary>
public class LocalFileStorage : IBackupStorage
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _rootPath;

    public LocalFileStorage(BackupConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        _rootPath = string.IsNullOrWhiteSpace(config.SubDirectory)
            ? config.Path
            : Path.Combine(config.Path, config.SubDirectory);
    }

    public async Task SaveBackupAsync(
        string contentType,
        BackupDocument document,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootPath);

        if (!IntuneContentTypes.FileNames.TryGetValue(contentType, out var fileName))
            fileName = $"{contentType.ToLowerInvariant()}.json";

        var filePath = Path.Combine(_rootPath, fileName);
        var json = JsonSerializer.Serialize(document, WriteOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public async Task<BackupDocument?> LoadBackupAsync(
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IntuneContentTypes.FileNames.TryGetValue(contentType, out var fileName))
            fileName = $"{contentType.ToLowerInvariant()}.json";

        var filePath = Path.Combine(_rootPath, fileName);
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<BackupDocument>(json, ReadOptions);
    }

    public Task<IReadOnlyList<string>> ListStoredContentTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var stored = new List<string>();

        if (!Directory.Exists(_rootPath))
            return Task.FromResult<IReadOnlyList<string>>(stored);

        foreach (var kvp in IntuneContentTypes.FileNames)
        {
            var filePath = Path.Combine(_rootPath, kvp.Value);
            if (File.Exists(filePath))
                stored.Add(kvp.Key);
        }

        return Task.FromResult<IReadOnlyList<string>>(stored);
    }

    public Task FinalizeExportAsync(string commitMessage, CancellationToken cancellationToken = default)
    {
        // No-op for local file storage
        return Task.CompletedTask;
    }
}
