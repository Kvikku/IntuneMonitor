using Azure.Core;
using IntuneMonitor.Authentication;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using IntuneMonitor.Reporting;
using IntuneMonitor.Storage;
using IntuneMonitor.UI;
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
        ConsoleUI.WriteHeader("Intune Export");
        _logger.LogInformation("=== Intune Export ===");

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
            _logger.LogError(ex, "Storage configuration error");
            return 0;
        }

        // Export from Graph
        var exporter = new IntuneExporter(credential, _loggerFactory);
        var progress = new Progress<string>(msg => _logger.LogDebug("{ProgressMessage}", msg));

        Dictionary<string, List<IntuneItem>> allItems;
        try
        {
            allItems = await ConsoleUI.StatusAsync("Fetching policies from Microsoft Graph...", async () =>
                await exporter.ExportAllAsync(types, progress, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export error");
            return 0;
        }

        // Save to storage
        int totalItems = 0;
        string tenantId = _config.Authentication.TenantId;
        var summaries = new List<ExportContentSummary>();
        var typeCounts = new List<(string ContentType, int Count)>();

        await ConsoleUI.StatusAsync("Saving to backup storage...", async () =>
        {
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
                typeCounts.Add((contentType, items.Count));

                summaries.Add(new ExportContentSummary
                {
                    ContentType = contentType,
                    ItemCount = items.Count,
                    ItemNames = items.Select(i => i.Name ?? i.Id ?? "(unknown)").ToList()
                });
            }
        });

        // Finalize (commit/push for Git storage)
        var commitMsg = $"Intune export {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC – {totalItems} items";
        await storage.FinalizeExportAsync(commitMsg, cancellationToken);

        // Generate HTML export report
        await WriteHtmlReportAsync(new ExportReport
        {
            GeneratedAt = DateTime.UtcNow,
            TenantId = tenantId,
            TenantName = tenantId,
            StorageType = _config.Backup.StorageType,
            BackupPath = _config.Backup.Path,
            ContentSummaries = summaries
        }, cancellationToken);

        // Summary output
        ConsoleUI.WriteExportSummary(totalItems, typeCounts);

        _logger.LogInformation("Export complete. {TotalItems} total item(s) exported", totalItems);
        ConsoleUI.Success($"Export complete — {totalItems} item(s) exported");
        return totalItems;
    }

    private async Task WriteHtmlReportAsync(ExportReport report, CancellationToken cancellationToken)
    {
        var outputPath = _config.Backup.HtmlExportReportPath;
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");
        outputPath = Reporting.ReportPath.WithTimestamp(outputPath, timestamp);

        try
        {
            await HtmlExportReportGenerator.WriteAsync(report, outputPath, cancellationToken);
            _logger.LogInformation("HTML export report written to: {OutputPath}", outputPath);

            if (_config.Backup.OpenHtmlExportReport)
                ReportWriter.OpenInBrowser(outputPath, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write HTML export report to '{OutputPath}'", outputPath);
        }
    }
}
