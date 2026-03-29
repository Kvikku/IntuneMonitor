using Azure.Core;
using IntuneMonitor.Authentication;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;
using IntuneMonitor.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Commands;

/// <summary>
/// Imports Intune content from the configured backup storage into the tenant.
/// </summary>
public class ImportCommand
{
    private readonly AppConfiguration _config;
    private readonly ILogger<ImportCommand> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ImportCommand(AppConfiguration config, ILoggerFactory? loggerFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<ImportCommand>();
    }

    /// <summary>
    /// Runs the import. Returns the number of successfully imported items.
    /// </summary>
    public async Task<int> RunAsync(
        IEnumerable<string>? contentTypes = null,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        ConsoleUI.WriteHeader("Intune Import");
        _logger.LogInformation("=== Intune Import ===");

        if (dryRun)
        {
            ConsoleUI.Warning("DRY RUN — no changes will be made to the tenant");
            _logger.LogInformation("[DRY RUN] No changes will be made to the tenant");
        }

        // Authenticate
        TokenCredential credential;
        try
        {
            credential = await ConsoleUI.StatusAsync("Authenticating...",
                () => Task.FromResult(CredentialFactory.Create(_config.Authentication)));
            ConsoleUI.Success($"Authenticated (method: {_config.Authentication.Method})");
            _logger.LogInformation("Authentication configured (method: {AuthMethod})", _config.Authentication.Method);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error");
            return 0;
        }

        // Determine content types
        var types = ContentTypeResolver.Resolve(contentTypes, _config.ContentTypes);

        // Load from storage
        IBackupStorage storage;
        try
        {
            storage = BackupStorageFactory.Create(_config.Backup, _loggerFactory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage error");
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
                _logger.LogWarning("No backup found for {ContentType}, skipping", contentType);
                continue;
            }

            _logger.LogInformation("Importing {ItemCount} {ContentType} item(s)...", backup.Items.Count, contentType);

            foreach (var item in backup.Items)
            {
                if (item.PolicyData == null)
                {
                    _logger.LogWarning("Skipping '{ItemName}' – no policy data in backup", item.Name);
                    continue;
                }

                if (dryRun)
                {
                    _logger.LogInformation("[DRY RUN] Would import: {ItemName}", item.Name);
                    successCount++;
                    continue;
                }

                try
                {
                    var imported = await importer.ImportItemAsync(item, cancellationToken);
                    _logger.LogInformation("Imported: {ImportedName}", imported);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import '{ItemName}'", item.Name);
                    errorCount++;
                }
            }
        }

        _logger.LogInformation("Import complete. {SuccessCount} succeeded, {ErrorCount} failed", successCount, errorCount);
        ConsoleUI.WriteImportSummary(successCount, errorCount);
        return successCount;
    }
}
