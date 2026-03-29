using System.Text.Json;
using IntuneMonitor.Comparison;
using IntuneMonitor.Config;
using IntuneMonitor.Models;
using IntuneMonitor.Reporting;
using IntuneMonitor.Storage;
using IntuneMonitor.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Commands;

/// <summary>
/// Compares two backup snapshots to detect differences without requiring live Graph API access.
/// </summary>
public class DiffCommand
{
    private static readonly JsonSerializerOptions ReportWriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppConfiguration _config;
    private readonly ILogger<DiffCommand> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public DiffCommand(AppConfiguration config, ILoggerFactory? loggerFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<DiffCommand>();
    }

    /// <summary>
    /// Compares backups at two different paths and returns a change report.
    /// </summary>
    /// <param name="sourcePath">Path to the "before" backup (baseline).</param>
    /// <param name="targetPath">Path to the "after" backup (current).</param>
    /// <param name="contentTypes">Optional content type filter.</param>
    /// <param name="htmlReportPath">Optional path to write an HTML report.</param>
    /// <param name="jsonReportPath">Optional path to write a JSON report.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ChangeReport"/> with the differences.</returns>
    public async Task<ChangeReport> RunAsync(
        string sourcePath,
        string targetPath,
        IEnumerable<string>? contentTypes = null,
        string? htmlReportPath = null,
        string? jsonReportPath = null,
        CancellationToken cancellationToken = default)
    {
        ConsoleUI.WriteHeader("Intune Backup Diff");
        _logger.LogInformation("=== Intune Backup Diff ===");
        _logger.LogInformation("Comparing backups: {SourcePath} → {TargetPath}", sourcePath, targetPath);

        // Create storage instances for both paths
        var sourceConfig = new BackupConfig { Path = sourcePath, StorageType = "LocalFile" };
        var targetConfig = new BackupConfig { Path = targetPath, StorageType = "LocalFile" };

        var sourceStorage = new LocalFileStorage(sourceConfig, _loggerFactory.CreateLogger<LocalFileStorage>());
        var targetStorage = new LocalFileStorage(targetConfig, _loggerFactory.CreateLogger<LocalFileStorage>());

        // Determine content types
        var types = ResolveContentTypes(contentTypes);

        // Compare
        var comparer = new PolicyComparer();
        var allChanges = new List<PolicyChange>();

        await ConsoleUI.StatusAsync("Comparing backup snapshots...", async () =>
        {
            foreach (var contentType in types)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceBackup = await sourceStorage.LoadBackupAsync(contentType, cancellationToken);
                var targetBackup = await targetStorage.LoadBackupAsync(contentType, cancellationToken);

                if (sourceBackup == null && targetBackup == null)
                    continue;

                // Treat target items as "live" and source as "backup" for comparison
                var targetItems = targetBackup?.Items ?? new List<IntuneItem>();
                var changes = comparer.Compare(contentType, targetItems, sourceBackup);

                if (changes.Count > 0)
                {
                    allChanges.AddRange(changes);
                    _logger.LogInformation("{ContentType}: {ChangeCount} change(s) detected", contentType, changes.Count);
                }
            }
        });

        var report = new ChangeReport
        {
            GeneratedAt = DateTime.UtcNow,
            TenantId = _config.Authentication.TenantId,
            TenantName = _config.Authentication.TenantId,
            Changes = allChanges
        };

        // Print to console
        if (report.HasChanges)
        {
            _logger.LogInformation("Diff complete: {TotalCount} change(s) — {AddedCount} added, {RemovedCount} removed, {ModifiedCount} modified",
                report.TotalCount, report.AddedCount, report.RemovedCount, report.ModifiedCount);
        }
        else
        {
            _logger.LogInformation("No differences found between the two backups");
        }

        ConsoleUI.WriteChangeReport(report);

        // Write reports
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");

        if (!string.IsNullOrWhiteSpace(jsonReportPath))
        {
            await WriteJsonReportAsync(report, jsonReportPath, timestamp, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(htmlReportPath))
        {
            await WriteHtmlReportAsync(report, htmlReportPath, timestamp, cancellationToken);
        }

        return report;
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

    private async Task WriteJsonReportAsync(ChangeReport report, string outputPath, string timestamp, CancellationToken cancellationToken)
    {
        outputPath = ReportPath.WithTimestamp(outputPath, timestamp);
        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(report, ReportWriteOptions);
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);
            _logger.LogInformation("JSON diff report written to: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write JSON diff report to '{OutputPath}'", outputPath);
        }
    }

    private async Task WriteHtmlReportAsync(ChangeReport report, string outputPath, string timestamp, CancellationToken cancellationToken)
    {
        outputPath = ReportPath.WithTimestamp(outputPath, timestamp);
        try
        {
            await HtmlReportGenerator.WriteAsync(report, outputPath, cancellationToken);
            _logger.LogInformation("HTML diff report written to: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write HTML diff report to '{OutputPath}'", outputPath);
        }
    }
}
