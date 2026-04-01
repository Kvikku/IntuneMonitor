using Azure.Core;
using IntuneMonitor.Authentication;
using IntuneMonitor.Comparison;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;
using IntuneMonitor.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Commands;

/// <summary>
/// Detects configuration drift and reverts modified/removed policies to their backed-up state.
/// Combines monitoring and import into a single remediation operation.
/// </summary>
public class RollbackCommand
{
    private readonly AppConfiguration _config;
    private readonly ILogger<RollbackCommand> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public RollbackCommand(AppConfiguration config, IHttpClientFactory httpClientFactory, ILoggerFactory? loggerFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<RollbackCommand>();
    }

    /// <summary>
    /// Detects drift and rolls back modified or removed policies to their backed-up state.
    /// </summary>
    /// <param name="contentTypes">Optional content type filter.</param>
    /// <param name="dryRun">When true, only reports what would be rolled back without making changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of policies successfully rolled back.</returns>
    public async Task<int> RunAsync(
        IEnumerable<string>? contentTypes = null,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        ConsoleUI.WriteHeader("Intune Rollback");
        _logger.LogInformation("=== Intune Rollback ===");

        if (dryRun)
        {
            ConsoleUI.Warning("DRY RUN — no changes will be made to the tenant");
            _logger.LogInformation("[DRY RUN] No changes will be made to the tenant");
        }

        // Authenticate
        TokenCredential credential;
        try
        {
            credential = await ConsoleUI.StatusAsync("Authenticating...", async () =>
            {
                await Task.CompletedTask;
                return CredentialFactory.Create(_config.Authentication);
            });
            _logger.LogInformation("Authentication configured (method: {AuthMethod})", _config.Authentication.Method);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error");
            return 0;
        }

        // Determine content types
        var types = ResolveContentTypes(contentTypes);

        // Load storage
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

        // Fetch current state from Graph
        var graphFactory = new GraphClientFactory(_httpClientFactory);
        var exporter = new IntuneExporter(credential, graphFactory);
        var progress = new Progress<string>(msg => _logger.LogDebug("{ProgressMessage}", msg));

        Dictionary<string, List<IntuneItem>> liveData;
        try
        {
            liveData = await ConsoleUI.StatusAsync("Fetching current state from Microsoft Graph...", async () =>
                await exporter.ExportAllAsync(types, progress, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch data from Graph");
            return 0;
        }

        // Compare against backup to find drift
        var comparer = new PolicyComparer();
        var rollbackItems = new List<(IntuneItem Item, PolicyChange Change)>();

        foreach (var (contentType, liveItems) in liveData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var backup = await storage.LoadBackupAsync(contentType, cancellationToken);
            if (backup == null) continue;

            var changes = comparer.Compare(contentType, liveItems, backup);

            foreach (var change in changes)
            {
                // Only rollback modified and removed policies (not newly added ones)
                if (change.ChangeType is ChangeType.Modified or ChangeType.Removed)
                {
                    var backupItem = backup.Items.FirstOrDefault(i =>
                        string.Equals(i.Id, change.PolicyId, StringComparison.OrdinalIgnoreCase));

                    if (backupItem?.PolicyData != null)
                    {
                        rollbackItems.Add((backupItem, change));
                    }
                }
            }
        }

        if (rollbackItems.Count == 0)
        {
            _logger.LogInformation("No drift detected — nothing to roll back");
            ConsoleUI.Success("No drift detected — nothing to roll back");
            return 0;
        }

        _logger.LogInformation("Found {DriftCount} drifted policies to roll back", rollbackItems.Count);

        // Perform rollback
        var importer = new IntuneImporter(credential, graphFactory, _loggerFactory);
        int successCount = 0;
        int errorCount = 0;

        foreach (var (item, change) in rollbackItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var icon = change.ChangeType == ChangeType.Modified ? "✎" : "✖";

            if (dryRun)
            {
                _logger.LogInformation("[DRY RUN] Would rollback {Icon} {ContentType}: {PolicyName} ({ChangeType})",
                    icon, change.ContentType, change.PolicyName, change.ChangeType);
                successCount++;
                continue;
            }

            try
            {
                _logger.LogInformation("Rolling back {Icon} {ContentType}: {PolicyName}...",
                    icon, change.ContentType, change.PolicyName);
                var result = await importer.ImportItemAsync(item, cancellationToken);
                if (result.Success)
                {
                    successCount++;
                    _logger.LogInformation("  → Rolled back successfully");
                }
                else
                {
                    _logger.LogError("  → Rollback failed for '{PolicyName}': {ErrorMessage}",
                        change.PolicyName, result.ErrorMessage);
                    errorCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback '{PolicyName}'", change.PolicyName);
                errorCount++;
            }
        }

        _logger.LogInformation("Rollback complete. {SuccessCount} succeeded, {ErrorCount} failed",
            successCount, errorCount);

        if (dryRun)
            ConsoleUI.Info($"Dry run complete — {successCount} policies would be rolled back");
        else
            ConsoleUI.Success($"Rollback complete — {successCount} succeeded, {errorCount} failed");

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
