using Azure.Core;
using IntuneMonitor.Authentication;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;

namespace IntuneMonitor.Commands;

/// <summary>
/// Imports Intune content from the configured backup storage into the tenant.
/// </summary>
public class ImportCommand
{
    private readonly AppConfiguration _config;

    public ImportCommand(AppConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Runs the import. Returns the number of successfully imported items.
    /// </summary>
    public async Task<int> RunAsync(
        IEnumerable<string>? contentTypes = null,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("=== Intune Import ===");

        if (dryRun)
            Console.WriteLine("[DRY RUN] No changes will be made to the tenant.");

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

        // Load from storage
        IBackupStorage storage;
        try
        {
            storage = BackupStorageFactory.Create(_config.Backup);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Storage error: {ex.Message}");
            return 0;
        }

        var importer = new IntuneImporter(credential);
        int successCount = 0;
        int errorCount = 0;

        foreach (var contentType in types)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var backup = await storage.LoadBackupAsync(contentType, cancellationToken);
            if (backup == null)
            {
                Console.WriteLine($"No backup found for {contentType}, skipping.");
                continue;
            }

            Console.WriteLine($"Importing {backup.Items.Count} {contentType} item(s)...");

            foreach (var item in backup.Items)
            {
                if (item.PolicyData == null)
                {
                    Console.WriteLine($"  ⚠ Skipping '{item.Name}' – no policy data in backup.");
                    continue;
                }

                if (dryRun)
                {
                    Console.WriteLine($"  [DRY RUN] Would import: {item.Name}");
                    successCount++;
                    continue;
                }

                try
                {
                    var imported = await importer.ImportItemAsync(item, cancellationToken);
                    Console.WriteLine($"  ✓ Imported: {imported}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  ✗ Failed to import '{item.Name}': {ex.Message}");
                    errorCount++;
                }
            }
        }

        Console.WriteLine($"\nImport complete. {successCount} succeeded, {errorCount} failed.");
        return successCount;
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
