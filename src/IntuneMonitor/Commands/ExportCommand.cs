using Azure.Core;
using IntuneMonitor.Authentication;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;

namespace IntuneMonitor.Commands;

/// <summary>
/// Exports Intune content to the configured backup storage.
/// </summary>
public class ExportCommand
{
    private readonly AppConfiguration _config;

    public ExportCommand(AppConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Runs the export and returns the number of items exported (0 on failure).
    /// </summary>
    public async Task<int> RunAsync(
        IEnumerable<string>? contentTypes = null,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("=== Intune Export ===");

        // Authenticate
        TokenCredential credential;
        try
        {
            credential = CredentialFactory.Create(_config.Authentication);
            Console.WriteLine($"Authentication configured (method: {_config.Authentication.Method})");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Authentication error: {ex.Message}");
            return 0;
        }

        // Determine content types
        var types = ResolveContentTypes(contentTypes);
        Console.WriteLine($"Content types to export: {string.Join(", ", types)}");

        // Create storage
        IBackupStorage storage;
        try
        {
            storage = BackupStorageFactory.Create(_config.Backup);
            Console.WriteLine($"Storage backend: {_config.Backup.StorageType} → {_config.Backup.Path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Storage configuration error: {ex.Message}");
            return 0;
        }

        // Export from Graph
        var exporter = new IntuneExporter(credential);
        var progress = new Progress<string>(msg => Console.WriteLine($"  {msg}"));

        Dictionary<string, List<IntuneItem>> allItems;
        try
        {
            allItems = await exporter.ExportAllAsync(types, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Export error: {ex.Message}");
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
            Console.WriteLine($"Saved {items.Count} {contentType} item(s).");
            totalItems += items.Count;
        }

        // Finalize (commit/push for Git storage)
        var commitMsg = $"Intune export {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC – {totalItems} items";
        await storage.FinalizeExportAsync(commitMsg, cancellationToken);

        Console.WriteLine($"\nExport complete. {totalItems} total item(s) exported.");
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
