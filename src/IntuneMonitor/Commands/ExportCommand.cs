using Azure.Core;
using IntuneMonitor.Authentication;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Commands;

/// <summary>
/// Exports Intune content to the configured backup storage.
/// </summary>
public class ExportCommand
{
    private readonly AppConfiguration _config;
    private readonly ILogger<ExportCommand> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ExportCommand(AppConfiguration config, ILoggerFactory? loggerFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<ExportCommand>();
    }

    /// <summary>
    /// Runs the export and returns the number of items exported (0 on failure).
    /// </summary>
    public async Task<int> RunAsync(
        IEnumerable<string>? contentTypes = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Intune Export ===");

        // Authenticate
        TokenCredential credential;
        try
        {
            credential = CredentialFactory.Create(_config.Authentication);
            _logger.LogInformation("Authentication configured (method: {AuthMethod})", _config.Authentication.Method);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error: {Message}", ex.Message);
            return 0;
        }

        // Determine content types
        var types = ResolveContentTypes(contentTypes);
        _logger.LogInformation("Content types to export: {ContentTypes}", string.Join(", ", types));

        // Create storage
        IBackupStorage storage;
        try
        {
            storage = BackupStorageFactory.Create(_config.Backup, _loggerFactory);
            _logger.LogInformation("Storage backend: {StorageType} → {BackupPath}", _config.Backup.StorageType, _config.Backup.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage configuration error: {Message}", ex.Message);
            return 0;
        }

        // Export from Graph
        var exporter = new IntuneExporter(credential);
        var progress = new Progress<string>(msg => _logger.LogDebug("{ProgressMessage}", msg));

        Dictionary<string, List<IntuneItem>> allItems;
        try
        {
            allItems = await exporter.ExportAllAsync(types, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export error: {Message}", ex.Message);
            return 0;
        }

        // Save to storage
        int totalItems = 0;
        string tenantId = _config.Authentication.TenantId;

        foreach (var (contentType, items) in allItems)
        {
            var document = new BackupDocument
            {
                ExportedAt = DateTime.UtcNow.ToString("o"),
                TenantId = tenantId,
                TenantName = tenantId,
                ContentType = contentType,
                Items = items
            };

            await storage.SaveBackupAsync(contentType, document, cancellationToken);
            _logger.LogInformation("Saved {ItemCount} {ContentType} item(s)", items.Count, contentType);
            totalItems += items.Count;
        }

        // Finalize (commit/push for Git storage)
        var commitMsg = $"Intune export {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC – {totalItems} items";
        await storage.FinalizeExportAsync(commitMsg, cancellationToken);

        _logger.LogInformation("Export complete. {TotalItems} total item(s) exported", totalItems);
        return totalItems;
    }

    private List<string> ResolveContentTypes(IEnumerable<string>? specified)
    {
        if (specified != null)
        {
            var list = specified.ToList();
            if (list.Count > 0) return list;
        }

        if (_config.ContentTypes?.Count > 0)
            return _config.ContentTypes;

        return IntuneContentTypes.All.ToList();
    }
}
