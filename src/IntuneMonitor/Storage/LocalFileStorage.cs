using System.Globalization;
using System.Text.Json;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Storage;

/// <summary>
/// Stores and loads Intune backups as JSON files on the local file system.
/// Each export run creates a timestamped folder containing per-type subfolders.
/// </summary>
public class LocalFileStorage : IBackupStorage
{
    private const string TimestampFormat = "yyyy-MM-dd_HHmmss";

    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorage> _logger;

    /// <summary>
    /// The timestamped folder used for the current export run.
    /// Created lazily on the first SaveBackupAsync call.
    /// </summary>
    private string? _currentRunPath;

    public LocalFileStorage(BackupConfig config, ILogger<LocalFileStorage>? logger = null)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        _rootPath = string.IsNullOrWhiteSpace(config.SubDirectory)
            ? config.Path
            : Path.Combine(config.Path, config.SubDirectory);

        _logger = logger ?? NullLogger<LocalFileStorage>.Instance;
    }

    public async Task SaveBackupAsync(
        string contentType,
        BackupDocument document,
        CancellationToken cancellationToken = default)
    {
        // Create a timestamped run folder on first save
        _currentRunPath ??= Path.Combine(_rootPath, DateTime.Now.ToString(TimestampFormat));

        var folderName = BackupFileHelpers.GetFolderName(contentType);
        var folderPath = Path.Combine(_currentRunPath, folderName);
        Directory.CreateDirectory(folderPath);

        // Write each item as an individual file named after the policy
        foreach (var item in document.Items)
        {
            var fileName = BackupFileHelpers.BuildFileName(item);
            var filePath = Path.Combine(folderPath, fileName);
            var json = JsonSerializer.Serialize(item, JsonDefaults.IndentedCamelCase);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }
    }

    public async Task<BackupDocument?> LoadBackupAsync(
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var latestRun = GetLatestRunPath();
        if (latestRun == null)
            return null;

        var folderName = BackupFileHelpers.GetFolderName(contentType);
        var folderPath = Path.Combine(latestRun, folderName);

        if (!Directory.Exists(folderPath))
            return null;

        var jsonFiles = Directory.GetFiles(folderPath, "*.json");
        if (jsonFiles.Length == 0)
            return null;

        var items = new List<IntuneItem>();
        foreach (var filePath in jsonFiles)
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var item = JsonSerializer.Deserialize<IntuneItem>(json, JsonDefaults.CaseInsensitiveRead);
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

        var latestRun = GetLatestRunPath();
        if (latestRun == null)
            return Task.FromResult<IReadOnlyList<string>>(stored);

        foreach (var kvp in IntuneContentTypes.FolderNames)
        {
            var folderPath = Path.Combine(latestRun, kvp.Value);
            if (Directory.Exists(folderPath) && Directory.GetFiles(folderPath, "*.json").Length > 0)
                stored.Add(kvp.Key);
        }

        return Task.FromResult<IReadOnlyList<string>>(stored);
    }

    public Task FinalizeExportAsync(string commitMessage, CancellationToken cancellationToken = default)
    {
        if (_currentRunPath != null)
            _logger.LogInformation("Backup saved to: {BackupPath}", _currentRunPath);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the most recent timestamped run folder.
    /// </summary>
    private string? GetLatestRunPath()
    {
        if (!Directory.Exists(_rootPath))
            return null;

        return Directory.GetDirectories(_rootPath)
            .Where(d => DateTime.TryParseExact(
                Path.GetFileName(d), TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
            .OrderByDescending(d => Path.GetFileName(d))
            .FirstOrDefault();
    }
}
