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
        var folderName = GetFolderName(contentType);
        var folderPath = Path.Combine(_rootPath, folderName);
        Directory.CreateDirectory(folderPath);

        // Remove files for items that no longer exist in the export
        CleanRemovedItems(folderPath, document.Items);

        // Write each item as an individual file named after the policy
        foreach (var item in document.Items)
        {
            var fileName = SanitizeFileName(item.Name ?? item.Id ?? "unknown") + ".json";
            var filePath = Path.Combine(folderPath, fileName);
            var json = JsonSerializer.Serialize(item, WriteOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }
    }

    public async Task<BackupDocument?> LoadBackupAsync(
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var folderName = GetFolderName(contentType);
        var folderPath = Path.Combine(_rootPath, folderName);

        if (!Directory.Exists(folderPath))
            return null;

        var jsonFiles = Directory.GetFiles(folderPath, "*.json");
        if (jsonFiles.Length == 0)
            return null;

        var items = new List<IntuneItem>();
        foreach (var filePath in jsonFiles)
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var item = JsonSerializer.Deserialize<IntuneItem>(json, ReadOptions);
            if (item != null)
                items.Add(item);
        }

        return new BackupDocument
        {
            ContentType = contentType,
            Items = items
        };
    }

    public Task<IReadOnlyList<string>> ListStoredContentTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var stored = new List<string>();

        if (!Directory.Exists(_rootPath))
            return Task.FromResult<IReadOnlyList<string>>(stored);

        foreach (var kvp in IntuneContentTypes.FolderNames)
        {
            var folderPath = Path.Combine(_rootPath, kvp.Value);
            if (Directory.Exists(folderPath) && Directory.GetFiles(folderPath, "*.json").Length > 0)
                stored.Add(kvp.Key);
        }

        return Task.FromResult<IReadOnlyList<string>>(stored);
    }

    public Task FinalizeExportAsync(string commitMessage, CancellationToken cancellationToken = default)
    {
        // No-op for local file storage
        return Task.CompletedTask;
    }

    private static string GetFolderName(string contentType) =>
        IntuneContentTypes.FolderNames.TryGetValue(contentType, out var folder)
            ? folder
            : contentType;

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }

    private static void CleanRemovedItems(string folderPath, List<IntuneItem> currentItems)
    {
        var expectedFiles = new HashSet<string>(
            currentItems.Select(i => SanitizeFileName(i.Name ?? i.Id ?? "unknown") + ".json"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var existingFile in Directory.GetFiles(folderPath, "*.json"))
        {
            if (!expectedFiles.Contains(Path.GetFileName(existingFile)))
                File.Delete(existingFile);
        }
    }
}
