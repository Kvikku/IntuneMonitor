using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Authentication;
using IntuneMonitor.Comparison;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using IntuneMonitor.Notifications;
using IntuneMonitor.Reporting;
using IntuneMonitor.Storage;
using IntuneMonitor.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Commands;

/// <summary>
/// Monitors Intune for changes by comparing current state against the backup.
/// Can run once or on a schedule.
/// </summary>
public class MonitorCommand
{
    private readonly AppConfiguration _config;
    private readonly ILogger<MonitorCommand> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public MonitorCommand(AppConfiguration config, ILoggerFactory? loggerFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<MonitorCommand>();
    }

    /// <summary>
    /// Runs one monitoring cycle (fetch, compare, report).
    /// </summary>
    public async Task<ChangeReport> RunOnceAsync(
        IEnumerable<string>? contentTypes = null,
        CancellationToken cancellationToken = default)
    {
        ConsoleUI.WriteHeader("Intune Monitor");
        _logger.LogInformation("=== Intune Monitor ===");
        _logger.LogInformation("Started at {StartTime} UTC", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

        // Authenticate
        TokenCredential credential;
        try
        {
            credential = await ConsoleUI.StatusAsync("Authenticating...",
                () => Task.FromResult(CredentialFactory.Create(_config.Authentication)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error");
            return EmptyReport();
        }

        // Determine content types
        var types = ContentTypeResolver.Resolve(contentTypes, _config.ContentTypes);

        // Load storage
        IBackupStorage storage;
        try
        {
            storage = BackupStorageFactory.Create(_config.Backup, _loggerFactory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage error");
            return EmptyReport();
        }

        // Fetch current state from Graph
        var exporter = new IntuneExporter(credential, _loggerFactory);
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
            return EmptyReport();
        }

        // Compare against backup
        var comparer = new PolicyComparer();
        var allChanges = new List<PolicyChange>();

        foreach (var (contentType, liveItems) in liveData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var backup = await storage.LoadBackupAsync(contentType, cancellationToken);
            var changes = comparer.Compare(contentType, liveItems, backup);

            if (changes.Count > 0)
            {
                allChanges.AddRange(changes);
            }
        }

        var report = new ChangeReport
        {
            GeneratedAt = DateTime.UtcNow,
            TenantId = _config.Authentication.TenantId,
            TenantName = _config.Authentication.TenantId,
            Changes = allChanges
        };

        PrintReport(report);
        ConsoleUI.WriteChangeReport(report);

        var reportTimestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");
        await WriteReportAsync(report, reportTimestamp, cancellationToken);
        await WriteHtmlReportAsync(report, reportTimestamp, cancellationToken);

        // Send notifications if configured
        await SendNotificationsAsync(report, cancellationToken);

        return report;
    }

    /// <summary>
    /// Runs the monitor on a recurring schedule until cancellation is requested.
    /// </summary>
    public async Task RunScheduledAsync(
        IEnumerable<string>? contentTypes = null,
        CancellationToken cancellationToken = default)
    {
        var intervalMinutes = _config.Monitor.IntervalMinutes;
        if (intervalMinutes <= 0)
        {
            // Single run
            await RunOnceAsync(contentTypes, cancellationToken);
            return;
        }

        _logger.LogInformation("Scheduled monitoring: running every {IntervalMinutes} minute(s). Press Ctrl+C to stop", intervalMinutes);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(contentTypes, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monitor run failed");
            }

            _logger.LogInformation("Next run in {IntervalMinutes} minute(s). Waiting...", intervalMinutes);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Monitoring stopped");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void PrintReport(ChangeReport report)
    {
        if (!report.HasChanges)
        {
            if (!_config.Monitor.ChangesOnly)
                _logger.LogInformation("No changes detected");
            return;
        }

        _logger.LogWarning("CHANGE REPORT – {ReportTime} UTC | Tenant: {TenantName} | Changes: {AddedCount} added, {RemovedCount} removed, {ModifiedCount} modified",
            report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"), report.TenantName,
            report.AddedCount, report.RemovedCount, report.ModifiedCount);

        var minSeverity = ParseSeverity(_config.Monitor.MinSeverity);

        foreach (var change in report.Changes)
        {
            if (change.Severity < minSeverity) continue;

            var icon = change.ChangeType switch
            {
                ChangeType.Added => "✚",
                ChangeType.Removed => "✖",
                ChangeType.Modified => "✎",
                _ => "?"
            };

            _logger.LogWarning("{Icon} [{ContentType}] {PolicyName} ({ChangeType}) | ID: {PolicyId}",
                icon, change.ContentType, change.PolicyName, change.ChangeType, change.PolicyId);

            if (!string.IsNullOrEmpty(change.Details))
                _logger.LogWarning("  Details: {Details}", change.Details);

            foreach (var field in change.FieldChanges.Take(10))
            {
                if (field.FieldPath.StartsWith("assignments.", StringComparison.OrdinalIgnoreCase))
                {
                    var action = field.FieldPath.Split('.').Last();
                    if (action.Equals("added", StringComparison.OrdinalIgnoreCase))
                        _logger.LogWarning("  Assignment added: {NewValue}", field.NewValue);
                    else if (action.Equals("removed", StringComparison.OrdinalIgnoreCase))
                        _logger.LogWarning("  Assignment removed: {OldValue}", field.OldValue);
                    else if (action.Equals("modified", StringComparison.OrdinalIgnoreCase))
                        _logger.LogWarning("  Assignment modified | Before: {OldValue} | After: {NewValue}",
                            field.OldValue, field.NewValue);
                }
                else
                {
                    _logger.LogWarning("  Field: {FieldPath} | Before: {OldValue} | After: {NewValue}",
                        field.FieldPath, Truncate(field.OldValue, 120), Truncate(field.NewValue, 120));
                }
            }

            if (change.FieldChanges.Count > 10)
                _logger.LogWarning("  ... and {RemainingCount} more field change(s)", change.FieldChanges.Count - 10);
        }
    }

    private async Task WriteReportAsync(ChangeReport report, string timestamp, CancellationToken cancellationToken)
    {
        var outputPath = _config.Monitor.ReportOutputPath;
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        outputPath = Reporting.ReportPath.WithTimestamp(outputPath, timestamp);
        await ReportWriter.WriteJsonAsync(report, outputPath, _logger, cancellationToken);
    }

    private async Task WriteHtmlReportAsync(ChangeReport report, string timestamp, CancellationToken cancellationToken)
    {
        var outputPath = _config.Monitor.HtmlReportOutputPath;
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        outputPath = Reporting.ReportPath.WithTimestamp(outputPath, timestamp);

        try
        {
            await HtmlReportGenerator.WriteAsync(report, outputPath, cancellationToken);
            _logger.LogInformation("HTML report written to: {OutputPath}", outputPath);

            if (_config.Monitor.OpenHtmlReport)
                ReportWriter.OpenInBrowser(outputPath, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write HTML report to '{OutputPath}'", outputPath);
        }
    }

    private static ChangeReport EmptyReport() =>
        new() { GeneratedAt = DateTime.UtcNow };

    private static ChangeSeverity ParseSeverity(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "critical" => ChangeSeverity.Critical,
            "warning" => ChangeSeverity.Warning,
            _ => ChangeSeverity.Info
        };

    private static string? Truncate(string? value, int maxLength) =>
        value == null ? "(null)" :
        value.Length <= maxLength ? value :
        value[..maxLength] + "...";

    private async Task SendNotificationsAsync(ChangeReport report, CancellationToken cancellationToken)
    {
        try
        {
            var senders = NotificationFactory.Create(_config.Notifications, _loggerFactory);
            if (senders.Count == 0) return;

            var service = new NotificationService(senders, _loggerFactory.CreateLogger<NotificationService>());
            await service.NotifyAsync(report, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notifications");
        }
    }
}
