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
        _logger.LogInformation("=== Intune Import ===");

        if (dryRun)
            _logger.LogInformation("[DRY RUN] No changes will be made to the tenant");

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

        // Load from storage
        IBackupStorage storage;
        try
        {
            storage = BackupStorageFactory.Create(_config.Backup, _loggerFactory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage error: {Message}", ex.Message);
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
                    _logger.LogError(ex, "Failed to import '{ItemName}': {Message}", item.Name, ex.Message);
                    errorCount++;
                }
            }
        }

        _logger.LogInformation("Import complete. {SuccessCount} succeeded, {ErrorCount} failed", successCount, errorCount);
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
